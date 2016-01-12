﻿Imports GalaSoft.MvvmLight
Imports GalaSoft.MvvmLight.Command
Imports Windows.UI.Core

Partial Public Class AppSettings
    Inherits ViewModelBase

    Dim settings As Windows.Storage.ApplicationDataContainer

    Const strServerIPKeyName As String = "strServerIP"
    Const strServerPortKeyName As String = "strServerPort"
    Const strUsernameKeyName As String = "strUserName"
    Const strUserPasswordKeyName As String = "strUserPassword"
    Const strMinimumNumberOfColumnsKeyName As String = "strMinimumNumberOfColumns"
    Const strShowMarqueeKeyName As String = "strShowMarquee"
    Const strShowFavouritesKeyName As String = "strShowFavourites"
    Const strShowAllDevicesKeyName As String = "strShowAllDevices"

#If DEBUG Then
    'PUT YOUR (TEST) SERVER DETAILS HERE IF YOU WANT TO DEBUG, AND NOT PROVIDE CREDENTIALS AND SERVER DETAILS EACH TIME
    Const strServerIPDefault = ""
    Const strServerPortDefault = ""
    Const strUsernameDefault = ""
    Const strUserPasswordDefault = ""
    Const strTimeOutDefault = 5
    Const strMinimumNumberOfColumnsDefault = 2
    Const strShowMarqueeDefault = "False"
    Const strShowFavouritesDefault = "True"
    Const strShowAllDevicesDefault = "False"
#Else
    'PROD SETTINGS
    Const strServerIPDefault = ""
    Const strServerPortDefault = ""
    Const strUsernameDefault = ""
    Const strUserPasswordDefault = ""
    Const strTimeOutDefault = 5
    Const strMinimumNumberOfColumnsDefault = 2
    Const strShowMarqueeDefault = "False"
    Const strShowFavouritesDefault = "True"
    Const strShowAllDevicesDefault = "False"
#End If

    Const strConnectionStatusDefault = False

    Public Sub New()
        settings = Windows.Storage.ApplicationData.Current.LocalSettings
    End Sub

    Public Property TestInProgress As Boolean
        Get
            Return _TestInProgress
        End Get
        Set(value As Boolean)
            _TestInProgress = value
            RaisePropertyChanged()
        End Set
    End Property
    Private Property _TestInProgress As Boolean


    Public ReadOnly Property NavigateBackCommand As RelayCommand
        Get
            Return New RelayCommand(Sub()
                                        Dim rootFrame As Frame = TryCast(Window.Current.Content, Frame)
                                        If rootFrame.CanGoBack Then rootFrame.GoBack()
                                    End Sub)
        End Get
    End Property

    Public ReadOnly Property TestConnectionCommand As RelayCommand
        Get
            Return New RelayCommand(Async Sub()
                                        TestInProgress = True
                                        TestConnectionResult = "Testing connection..."
                                        WriteToDebug("TestConnectionCommand", "executed")
                                        If ContainsValidIPDetails() Then
                                            Dim response As retvalue = Await (New Plans).Load()
                                            If response.issuccess Then
                                                TestConnectionResult = "Hurray !"
                                            Else
                                                TestConnectionResult = String.Format("Hmm..doesn't work : {0}", response.err)
                                            End If
                                        Else
                                            TestConnectionResult = "Server IP/Port not valid !"
                                        End If
                                        TestInProgress = False
                                    End Sub)
        End Get
    End Property

    Public Property TestConnectionResult As String
        Get
            Return _TestConnectionResult
        End Get
        Set(value As String)
            _TestConnectionResult = value
            RaisePropertyChanged()
        End Set
    End Property
    Private Property _TestConnectionResult As String

    'Checks if the Server IP and the Server Port are valid
    Public Function ContainsValidIPDetails() As Boolean
        Dim tmpIPAddress As Net.IPAddress
        If Net.IPAddress.TryParse(ServerIP, tmpIPAddress) Then

            If ServerPort.Length > 0 AndAlso ServerPort.All(Function(x) Char.IsDigit(x)) AndAlso CType(ServerPort, Integer) <= 65535 Then
                Return True
            Else
                Return False
            End If
        Else
            Return False
        End If
    End Function


    Public Function GetFullURL() As String
        Return "http://" + ServerIP + ":" + ServerPort
    End Function

    Public Function AddOrUpdateValue(Key As String, value As Object)
        Dim valueChanged As Boolean = False

        If value Is Nothing Then Return False
        If settings.Values.ContainsKey(Key) Then
            settings.Values(Key) = value
            valueChanged = True

        Else
            settings.Values.Add(Key, value)
            valueChanged = True
        End If
        Return valueChanged
    End Function

    Public Function GetValueOrDefault(Of T)(Key As String, defaultValue As T) As T

        Dim value As T
        ' If the key exists, retrieve the value.
        If Not settings.Values(Key) Is Nothing Then
            value = DirectCast(settings.Values(Key), T)
        Else
            ' Otherwise, use the default value.
            value = defaultValue
        End If
        Return value
    End Function

    Public Sub Save()
        'settings.Save()
    End Sub

    Private _YesNoList As List(Of String) = New List(Of String)({"True", "False"}).ToList
    Public ReadOnly Property YesNoChoices As List(Of String)
        Get
            Return _YesNoList
        End Get
    End Property

    Public Property ShowFavourites As String
        Get
            Return GetValueOrDefault(Of String)(strShowFavouritesKeyName, strShowFavouritesDefault)
        End Get
        Set(value As String)
            If AddOrUpdateValue(strShowFavouritesKeyName, value) Then
                Save()
            End If
        End Set
    End Property
    Public Property ShowAllDevices As String
        Get
            Return GetValueOrDefault(Of String)(strShowAllDevicesKeyName, strShowAllDevicesDefault)
        End Get
        Set(value As String)
            If AddOrUpdateValue(strShowAllDevicesKeyName, value) Then
                Save()
            End If
        End Set
    End Property

    Public Property ShowMarquee As String
        Get
            Return GetValueOrDefault(Of String)(strShowMarqueeKeyName, strShowMarqueeDefault)
        End Get
        Set(value As String)
            If AddOrUpdateValue(strShowMarqueeKeyName, value) Then
                Save()
            End If
        End Set
    End Property

    Private _NumberOfColumns As List(Of Integer) = New List(Of Integer)({1, 2, 3, 4, 5, 6, 7, 8, 9, 10}).ToList
    Public ReadOnly Property NumberOfColumnsChoices As List(Of Integer)
        Get
            Return _NumberOfColumns
        End Get
    End Property

    Public Property MinimumNumberOfColumns As Integer
        Get
            Return GetValueOrDefault(Of Integer)(strMinimumNumberOfColumnsKeyName, strMinimumNumberOfColumnsDefault)
        End Get
        Set(value As Integer)
            If AddOrUpdateValue(strMinimumNumberOfColumnsKeyName, value) Then
                Save()
            End If
        End Set
    End Property

    Public Property ServerPort As String
        Get
            Return GetValueOrDefault(Of String)(strServerPortKeyName, strServerPortDefault)
        End Get
        Set(value As String)
            If AddOrUpdateValue(strServerPortKeyName, value) Then
                Save()
            End If
        End Set
    End Property



    Public Property ServerIP As String
        Get
            Return GetValueOrDefault(Of String)(strServerIPKeyName, strServerIPDefault)
        End Get
        Set(value As String)
            If AddOrUpdateValue(strServerIPKeyName, value) Then
                Save()
            End If
        End Set
    End Property

    Public Property Password As String
        Get
            Return GetValueOrDefault(Of String)(strUserPasswordKeyName, strUserPasswordDefault)
        End Get
        Set(value As String)
            If AddOrUpdateValue(strUserPasswordKeyName, value) Then
                Save()
            End If
        End Set
    End Property

    Public Property Username As String
        Get
            Return GetValueOrDefault(Of String)(strUsernameKeyName, strUsernameDefault)
        End Get
        Set(value As String)
            If AddOrUpdateValue(strUsernameKeyName, value) Then
                Save()
            End If
        End Set
    End Property

End Class



Public NotInheritable Class AppSettingsPage
    Inherits Page

    Dim app As App = CType(Application.Current, App)

    Protected Overrides Sub OnNavigatedTo(e As NavigationEventArgs)
        Me.DataContext = app.myViewModel.TiczSettings
        Dim rootFrame As Frame = CType(Window.Current.Content, Frame)
        If rootFrame.CanGoBack Then
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible
        Else
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed
        End If
    End Sub


    Protected Overrides Sub OnNavigatedFrom(e As NavigationEventArgs)

    End Sub


    Public Sub New()
        InitializeComponent()
    End Sub
End Class
