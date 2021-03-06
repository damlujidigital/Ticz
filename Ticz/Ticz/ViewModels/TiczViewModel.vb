﻿Imports System.Threading
Imports GalaSoft.MvvmLight
Imports GalaSoft.MvvmLight.Command
Imports Newtonsoft.Json
Imports Windows.Foundation.Metadata
Imports Windows.UI
Imports Windows.Web.Http

Public Class TiczViewModel
    Inherits ViewModelBase

    Public Property HasHardwareBackButton As Boolean = If(ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"), True, False)
    Public Property ActiveContentDialog As CustomContentDialog
    Public Property Cameras As New CameraListViewModel
    Public Property DomoConfig As New Domoticz.Config
    Public Property DomoSunRiseSet As New Domoticz.SunRiseSet
    Public Property DomoVersion As New Domoticz.Version
    'Public Property DomoRooms As New Domoticz.Plans
    Public Property DomoSettings As New Domoticz.Settings
    Public Property DomoSecPanel As New SecurityPanelViewModel
    Public Property Variables As New VariableListViewModel
    Public Property Rooms As New RoomsViewModel

    Public Property TiczSettings As New TiczSettings
    Public Property IdleTimer As New IdleTimerViewModel
    'Public Property TiczRoomConfigs As New TiczStorage.RoomConfigurations

    Public Property TiczMenu As New TiczMenuSettings
    Public Property Notify As New ToastMessageViewModel
    Public Property IsRefreshing As Boolean
        Get
            Return _IsRefreshing
        End Get
        Set(value As Boolean)
            _IsRefreshing = value
            RaisePropertyChanged("IsRefreshing")
        End Set
    End Property
    Private Property _IsRefreshing As Boolean
    Public Property RoomIsLoading As Boolean 'Bool that identifies if a room is being populated with devices. Avoid doing a refresh when this is true

    Public Property LastRefresh As DateTime

    'Properties used for the background refresher
    Public Property TiczRefresher As Task
    Public ct As CancellationToken
    Public tokenSource As New CancellationTokenSource()


    Public ReadOnly Property ViewModelLoadedCommand As RelayCommand(Of Object)
        Get
            Return New RelayCommand(Of Object)(Async Sub(x)
                                                   WriteToDebug("TiczViewModel.ViewModelLoadedCommand()", "executed")
                                                   Await Load()
                                               End Sub)
        End Get
    End Property

    Public Sub New()
    End Sub

    Public Async Sub RoomSelected(sender As Object, e As SelectionChangedEventArgs)
        Dim selectedRoom As RoomViewModel = TryCast(sender, ListView).SelectedItem
        If Not selectedRoom Is Nothing Then
            Await Notify.Update(False, "Loading room...", 1, False, 0)
            RoomIsLoading = True
            If TiczMenu.IsMenuOpen Then TiczMenu.IsMenuOpen = False
            'Don't start loading the room if the background refresh task is still running
            While IsRefreshing
                Await Task.Delay(100)
            End While
            Dim PreviousRoom = Rooms.ActiveRoom
            Await Rooms.SetActiveRoom(selectedRoom.RoomIDX)
            RoomIsLoading = False
            Notify.Clear()
        End If
    End Sub

    Public Async Sub ShowSecurityPanel()
        WriteToDebug("TiczMenuSettings.ShowSecurityPanel()", "executed")
        Me.TiczMenu.IsMenuOpen = False
        Me.IdleTimer.StopCounter()
        ActiveContentDialog = New TiczContentDialog
        ActiveContentDialog.Title = "Security Panel"
        Dim details As New ucSecurityPanel()
        ActiveContentDialog.Content = details
        Await ActiveContentDialog.ShowAsync()
        Me.IdleTimer.StartCounter()
    End Sub


    Public Async Sub ShowVariables()
        WriteToDebug("TiczMenuSettings.ShowCameras()", "executed")
        Me.TiczMenu.IsMenuOpen = False
        Me.IdleTimer.StopCounter()
        Await Notify.Update(False, "Loading Domoticz variables...", 0, False, 0)
        If Not (Await Variables.Load()).issuccess Then
            Await Notify.Update(True, "Error loading Domoticz variables...", 1, False, 0)
        Else
            ActiveContentDialog = New TiczContentDialog
            ActiveContentDialog.Title = "Domoticz Variables"
            Dim vlist As VariableListViewModel = CType(Application.Current, Application).myViewModel.Variables
            Dim uclist As New ucVariableList
            uclist.DataContext = vlist
            ActiveContentDialog.Content = uclist
            Notify.Clear()
            Await ActiveContentDialog.ShowAsync()
            ActiveContentDialog = Nothing
            Me.IdleTimer.StartCounter()
        End If

    End Sub

    Public Async Sub ShowScreenSaver()
        WriteToDebug("TiczMenuSettings.ShowScreenSaver()", "executed")
        Me.TiczMenu.IsMenuOpen = False
        ActiveContentDialog = New TiczContentDialog
        ActiveContentDialog.HeaderVisibility = Visibility.Collapsed
        ActiveContentDialog.Background = New SolidColorBrush(Colors.Black)
        ActiveContentDialog.BackgroundOpacity = 1
        Dim touchHandler = New TappedEventHandler(Sub(s, e)
                                                      ActiveContentDialog.Hide()
                                                  End Sub)
        ActiveContentDialog.AddHandler(UIElement.TappedEvent, touchHandler, True)
        Dim vlist As VariableListViewModel = CType(Application.Current, Application).myViewModel.Variables
        Dim ucScreenSaver As New ucScreenSaver
        Dim sSaver As New ScreenSaverViewModel(Window.Current.Bounds)
        ucScreenSaver.DataContext = sSaver
        ActiveContentDialog.Content = ucScreenSaver
        Notify.Clear()
        sSaver.StartRefresh()
        Await ActiveContentDialog.ShowAsync()
        ActiveContentDialog = Nothing
        IdleTimer.ResetCounter()
        sSaver.StopRefresh()
        sSaver = Nothing
    End Sub

    Public Async Sub ShowCameras()
        WriteToDebug("TiczMenuSettings.ShowCameras()", "executed")
        Me.TiczMenu.IsMenuOpen = False
        Me.IdleTimer.StopCounter()
        ActiveContentDialog = New TiczContentDialog
        ActiveContentDialog.Title = "Cameras"
        Dim clist As New ucCameraList()
        'Before Showing the cams, try to capture the latest frame for each
        For Each c In Cameras
            Await c.GetFrameFromJPG()
        Next
        clist.DataContext = Cameras
        ActiveContentDialog.Content = clist
        Await ActiveContentDialog.ShowAsync()
        'Stop refreshing any camera that exists
        For Each c In Cameras
            c.StopRefresh()
        Next
        Me.IdleTimer.StartCounter()
    End Sub


    Public Async Sub ShowAbout()
        WriteToDebug("TiczMenuSettings.ShowAbout()", "executed")
        Me.TiczMenu.IsMenuOpen = False
        Me.IdleTimer.StopCounter()
        ActiveContentDialog = New TiczContentDialog
        ActiveContentDialog.Title = "About Ticz..."
        Dim about As New ucAbout()
        ActiveContentDialog.Content = about
        Await ActiveContentDialog.ShowAsync()
        Me.IdleTimer.StartCounter()
    End Sub

    Public Async Sub StartRefresh()
        WriteToDebug("TiczViewModel.StartRefresh()", "")
        If TiczRefresher Is Nothing OrElse TiczRefresher.IsCompleted Then
            If TiczSettings.SecondsForRefresh > 0 Then
                WriteToDebug("TiczViewModel.StartRefresh()", String.Format("every {0} seconds", TiczSettings.SecondsForRefresh))
                tokenSource = New CancellationTokenSource
                ct = tokenSource.Token
                TiczRefresher = Await Task.Factory.StartNew(Function() PerformAutoRefresh(ct), ct)
            Else
                WriteToDebug("TiczViewModel.StartRefresh()", "SecondsForRefresh = 0, not starting background task...")
            End If
        Else
            If ct.IsCancellationRequested Then
                'The Refresh task is still running, but cancellation is requested. Let it finish, before we restart it
                Dim s As New Stopwatch
                s.Start()
                While Not TiczRefresher.IsCompleted
                    Await Task.Delay(10)
                End While
                s.Stop()
                WriteToDebug("TiczViewModel.StartRefresh()", String.Format("refresher had to wait for {0} ms for previous task to complete", s.ElapsedMilliseconds))
                StartRefresh()
            End If
        End If
    End Sub

    Public Sub StopRefresh()
        If ct.CanBeCanceled Then
            tokenSource.Cancel()
        End If
        WriteToDebug("TiczViewModel.StopRefresh()", "")
    End Sub

    Public Async Function PerformAutoRefresh(ct As CancellationToken) As Task
        Try
            While Not ct.IsCancellationRequested
                Dim i As Integer = 0
                If TiczSettings.SecondsForRefresh = 0 Then
                    While i < 5 * 1000
                        Await Task.Delay(100)
                        i += 100
                        If ct.IsCancellationRequested Then WriteToDebug("TiczViewModel.PerformAutoRefresh", "cancelling") : Exit While
                    End While
                Else
                    While i < TiczSettings.SecondsForRefresh * 1000
                        Await Task.Delay(100)
                        i += 100
                        If ct.IsCancellationRequested Then WriteToDebug("TiczViewModel.PerformAutoRefresh", "cancelling") : Exit While
                    End While
                End If
                If ct.IsCancellationRequested Then Exit While
                If TiczSettings.SecondsForRefresh > 0 Then Await Refresh(False)
            End While
        Catch ex As Exception
            Notify.Update(True, "AutoRefresh task crashed :(", 2, False, 4)
        End Try

    End Function

    ''' <summary>
    ''' Triggers a full manual refresh of the current Room's devices
    ''' </summary>
    ''' <returns></returns>
    Public Async Function ManualRefresh() As Task
        Await Refresh(True)
    End Function

    Public Async Function Reload() As Task
        WriteToDebug("TiczViewModel.Reload()", "executed")
        TiczMenu.IsMenuOpen = False
        Await Load()
    End Function


    Public Async Function Refresh(Optional LoadAllUpdates As Boolean = False) As Task
        If Not RoomIsLoading Then
            'Set IsRefreshing to true, which needs to be done on the GUI thread as the refresh indicator is triggered by this value
            Await RunOnUIThread(Sub()
                                    IsRefreshing = True
                                End Sub)
            Dim sWatch = Stopwatch.StartNew()
            'Refresh the Sunset/Rise values, Exit refresh if getting this fails
            If Not (Await DomoSunRiseSet.Load()).issuccess Then Exit Function
            'Refresh the Security Panel Status, exit refresh if this fails
            If Not (Await DomoSecPanel.GetSecurityStatus).issuccess Then Exit Function

            'Get all devices for this room that have been updated since the LastRefresh (Domoticz will tell you which ones)
            Dim dev_response, grp_response As New HttpResponseMessage
            'Hack in case we're looking at the "All Devices" room, we need to download status for all devices regardless of the room
            Select Case Rooms.ActiveRoom.RoomIDX
                Case 12321
                    dev_response = Await Task.Run(Function() (New Domoticz).DownloadJSON((New DomoApi).getAllDevices()))
                    grp_response = Await Task.Run(Function() (New Domoticz).DownloadJSON((New DomoApi).getAllScenes()))

                Case 0
                    dev_response = Await Task.Run(Function() (New Domoticz).DownloadJSON((New DomoApi).getFavouriteDevices()))
                    'grp_response = Await Task.Run(Function() (New Domoticz).DownloadJSON((New DomoApi).getAllScenes()))
                Case Else
                    dev_response = Await Task.Run(Function() (New Domoticz).DownloadJSON((New DomoApi).getAllDevicesForRoom(Rooms.ActiveRoom.RoomIDX, LoadAllUpdates)))
                    grp_response = Await Task.Run(Function() (New Domoticz).DownloadJSON((New DomoApi).getAllScenesForRoom(Rooms.ActiveRoom.RoomIDX)))
            End Select

            'Collect all updated groups/scenes and devices into a single list
            Dim devicesToRefresh As New List(Of DeviceModel)
            If dev_response.IsSuccessStatusCode AndAlso Not dev_response.Content Is Nothing Then
                devicesToRefresh.AddRange((JsonConvert.DeserializeObject(Of DevicesModel)(Await dev_response.Content.ReadAsStringAsync)).result)
            End If
            If grp_response.IsSuccessStatusCode AndAlso Not grp_response.Content Is Nothing Then
                devicesToRefresh.AddRange((JsonConvert.DeserializeObject(Of DevicesModel)(Await grp_response.Content.ReadAsStringAsync)).result)
            End If

            'Iterate through the list of updated devices, find the matching device in the room and update it
            If devicesToRefresh.Count > 0 Then
                For Each d In devicesToRefresh
                    Dim deviceToUpdate As DeviceViewModel
                    Dim GroupIndex, ItemIndex As Integer
                    GroupIndex = Rooms.ActiveRoom.GetGroupIndexForDevice(d.idx, d.Name)
                    ItemIndex = Rooms.ActiveRoom.GetItemIndexForDevice(d.idx, d.Name)
                    If GroupIndex > -1 AndAlso ItemIndex > -1 Then
                        deviceToUpdate = Rooms.ActiveRoom.GroupedDevices(GroupIndex)(ItemIndex)
                        If Not deviceToUpdate Is Nothing Then
                            'WriteToDebug("TiczViewModel.Refresh()", String.Format("Updating device {0} / {1}", d.idx, d.Name))
                            Await RunOnUIThread(Async Sub()
                                                    Await deviceToUpdate.Update(d)
                                                End Sub)
                        End If
                    Else
                        'WriteToDebug("TiczViewModel.Refresh()", String.Format("Skipping update for device {0} / {1}", d.idx, d.Name))
                    End If

                Next
            Else
                If Not grp_response.IsSuccessStatusCode Or Not dev_response.IsSuccessStatusCode Then
                    Await Notify.Update(True, "Error loading refreshed devices/groups...", 2, False, 2)
                End If
            End If

            'Clear the Notification
            sWatch.Stop()
            If dev_response.IsSuccessStatusCode AndAlso grp_response.IsSuccessStatusCode Then
                'But only if the amount of time passed for the Refresh Is around 500ms (approx. time for the animation showing "Refreshing" to be on the screen
                WriteToDebug("TiczViewModel.Refresh()", String.Format("Refresh took {0} ms", sWatch.ElapsedMilliseconds))
                If sWatch.ElapsedMilliseconds < 1000 Then
                    Await Task.Delay(1000 - sWatch.ElapsedMilliseconds)
                End If
                Notify.Clear()
            End If
            dev_response = Nothing : grp_response = Nothing
            LastRefresh = Date.Now.ToUniversalTime
            Await RunOnUIThread(Sub()
                                    IsRefreshing = False
                                End Sub)
        End If
    End Function

    ''' <summary>
    ''' Performs initial loading of all Data for Ticz. Ensures all data is cleared before reloading
    ''' </summary>
    ''' <returns></returns>
    Public Async Function Load() As Task
        If Not TiczSettings.ContainsValidIPDetails Then
            Await Notify.Update(True, "IP/Port settings not valid", 2, False, 0)
            TiczMenu.ActiveMenuContents = "Server settings"
            Await Task.Delay(500)
            TiczMenu.IsMenuOpen = True
            Exit Function
        End If
        Await Notify.Update(False, "Loading...", 0, True, 0)
        RoomIsLoading = True

        'Load Domoticz General Config from Domoticz
        Await Notify.Update(False, "Loading Domoticz configuration...", 0, False, 0)
        If Not (Await DomoConfig.Load()).issuccess Then Exit Function

        Await Notify.Update(False, "Loading Domoticz settings...", 0, False, 0)
        If Not (Await DomoSettings.Load()).issuccess Then Exit Function

        'Load Domoticz Sunrise/set Info from Domoticz
        Await Notify.Update(False, "Loading Domoticz Sunrise/Set...", 0, False, 0)
        If Not (Await DomoSunRiseSet.Load()).issuccess Then Exit Function

        'Load Version Information from Domoticz
        Await Notify.Update(False, "Loading Domoticz version info...", 0, False, 0)
        If Not (Await DomoVersion.Load()).issuccess Then Exit Function

        'Load Cameras from Domoticz
        Await Notify.Update(False, "Loading cameras...", 0, False, 0)
        If Not (Await Cameras.Load()).issuccess Then
            Await Notify.Update(True, "Error loading cameras...", 1, False, 0)
            Await Task.Delay(1000)
        End If

        'Load the Room/Floorplans from the Domoticz Server
        'Await Notify.Update(False, "Loading Domoticz rooms...", 0)
        'If Not (Await DomoRooms.Load()).issuccess Then
        '    Await Notify.Update(True, "Error loading Domoticz rooms...", 1, False, 0)
        'End If



        'TODO : MOVE SECPANEL STUFF TO RIGHT PLACE
        Await Notify.Update(False, "Loading Domoticz Security Panel Status...", 0, False, 0)
        Await DomoSecPanel.GetSecurityStatus()

        'Load the Room Configurations from Storage and Domoticz Server
        Await Notify.Update(False, "Loading Ticz Rooms...", 0, False, 0)
        Await Rooms.Load()
        'If Not Await TiczRoomConfigs.LoadRoomConfigurations() Then
        '    Await Task.Delay(2000)
        'End If

        'Await LoadRoom()

        'Save the (potentially refreshhed) roomconfigurations again
        Await Notify.Update(False, "Saving Ticz Room configuration...", 0, False, 0)
        'Await TiczRoomConfigs.SaveRoomConfigurations()
        LastRefresh = Date.Now.ToUniversalTime
        StartRefresh()

        Notify.Clear()

        'Start IdleTimeCounter
        IdleTimer.StartCounter()

        RoomIsLoading = False
    End Function
End Class