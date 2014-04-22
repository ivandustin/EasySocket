Imports System.Net
Imports System.Net.Sockets
Imports System.Text

Public Class EasySocket

    Protected Shared Property maxBufferSize As Integer = 1024 * 8
    Protected Shared Property backlog As Integer = 1000

    Protected Class EasySocketMessage
        Private Property name As String
        Private Property msg As String
        Private Property bytes As Byte()

        Public Sub New(name As String, msg As String)
            Me.name = name
            Me.msg = msg
            Me.bytes = serialize(name, msg)
        End Sub

        Public Sub New(bytes() As Byte)
            Me.bytes = bytes
            Dim data As String() = parse(bytes)
            Me.name = data(0)
            Me.msg = data(1)
        End Sub

        Public Function getName() As String
            Return name
        End Function

        Public Function getMsg() As String
            Return msg
        End Function

        Public Function getBytes() As Byte()
            Return bytes
        End Function

        Private Function serialize(name As String, msg As String) As Byte()
            Dim data = name & ":::" & msg
            Return Encoding.UTF8.GetBytes(data)
        End Function

        Private Function parse(bytes() As Byte) As String()
            Dim dataStr As String = Encoding.UTF8.GetString(bytes).Replace(Chr(0), "")
            Dim idx As Integer = dataStr.IndexOf(":::")

            If idx = -1 Then
                Return {"", dataStr}
            End If

            Dim name As String = dataStr.Substring(0, idx)
            Dim msg As String = dataStr.Substring(idx + 3)
            Return {name, msg}
        End Function
    End Class

    Public Shared Sub setBufferSize(size As Integer)
        maxBufferSize = size
    End Sub

    Public Shared Sub setBacklog(count As Integer)
        backlog = count
    End Sub

    Public Shared Function connect(server As String, port As Integer) As EasySocketClient
        Return New EasySocketClient(server, port)
    End Function

    Public Shared Function listen(port As Integer) As EasySocketServer
        Return New EasySocketServer(port)
    End Function

End Class

Public Class EasySocketClient
    Inherits EasySocket

    Public Event Receive(name As String, msg As String, socket As EasySocketClient)
    Public Event Disconnected(client As EasySocketClient)

    Private socket As Socket

    Public id As String = Guid.NewGuid().ToString()

    Public Function isConnected() As Boolean
        Return socket.Connected
    End Function

    Public Sub New(server As String, port As Integer)
        Dim ipaddress As IPAddress = ipaddress.Parse(server)
        Dim endpoint As New IPEndPoint(ipaddress, port)
        socket = New Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        socket.Connect(endpoint)
        startReceiving()
    End Sub

    Public Sub New(socket As Socket)
        Me.socket = socket
        startReceiving()
    End Sub

    Private Sub startReceiving()
        If Not socket.Connected Then
            Return
        End If

        Dim buffer(maxBufferSize) As Byte
        Dim callback As New AsyncCallback(AddressOf receiveBuffer)
        socket.BeginReceive(buffer, 0, maxBufferSize, SocketFlags.None, callback, buffer)
    End Sub

    Public Sub send(name As String, msg As String)
        Dim esmsg As New EasySocketMessage(name, msg)
        Socket.Send(esmsg.getBytes())
    End Sub

    Public Sub disconnect()
        socket.Close()
        RaiseEvent Disconnected(Me)
    End Sub

    Protected Sub receiveBuffer(ar As IAsyncResult)
        If Not socket.Connected Then
            Return
        End If

        Dim bytesReceived As Integer

        Try
            bytesReceived = socket.EndReceive(ar)
        Catch ex As SocketException
            disconnect()
            Return
        End Try

        If bytesReceived = 0 Then
            disconnect()
            Return
        End If

        Dim buffer As Byte() = CType(ar.AsyncState, Byte())
        Dim esmsg As New EasySocketMessage(buffer)
        RaiseEvent Receive(esmsg.getName(), esmsg.getMsg(), Me)
        startReceiving()
    End Sub

End Class


Public Class EasySocketServer
    Inherits EasySocket

    Public Event Receive(name As String, msg As String, client As EasySocketClient)
    Public Event ClientConnected(client As EasySocketClient)
    Public Event ClientDisconnected(client As EasySocketClient)

    Private socket As Socket

    'Public Function isListening() As Boolean
    '    Return socket.Connected
    'End Function

    Public Sub New(port As Integer)
        socket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        Dim ipaddress As IPAddress = ipaddress.Parse("127.0.0.1")
        Dim endpoint As New IPEndPoint(ipaddress, port)
        socket.Bind(endpoint)
        socket.Listen(backlog)
        beginAcceptingConnections()
    End Sub

    Private Sub beginAcceptingConnections()
        Dim callback As New AsyncCallback(AddressOf acceptConnection)
        socket.BeginAccept(callback, socket)
    End Sub

    Private Sub acceptConnection(ar As IAsyncResult)
        Dim serversocket As Socket = CType(ar.AsyncState, Socket)
        Dim buffer(maxBufferSize) As Byte
        Dim connectingSocket As Socket = serversocket.EndAccept(ar)
        Dim client As New EasySocketClient(connectingSocket)
        AddHandler client.Receive, AddressOf receiveBuffer
        AddHandler client.Disconnected, AddressOf clientDisconnect
        RaiseEvent ClientConnected(client)
        beginAcceptingConnections()
    End Sub

    Private Sub receiveBuffer(name As String, msg As String, client As EasySocketClient)
        RaiseEvent Receive(name, msg, client)
    End Sub

    Private Sub clientDisconnect(client As EasySocketClient)
        RaiseEvent ClientDisconnected(client)
    End Sub

    Public Sub disconnect()
        socket.Close()
    End Sub
End Class