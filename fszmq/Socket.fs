﻿(*-------------------------------------------------------------------------
Copyright (c) Paulmichael Blasucci.                                        
                                                                           
This source code is subject to terms and conditions of the Apache License, 
Version 2.0. A copy of the license can be found in the License.html file   
at the root of this distribution.                                          
                                                                           
By using this source code in any fashion, you are agreeing to be bound     
by the terms of the Apache License, Version 2.0.                           
                                                                           
You must not remove this notice, or any other, from this software.         
-------------------------------------------------------------------------*)
namespace fszmq

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

/// Encapsulates data generated by various ZMQ monitoring events
type SocketEvent =
  { Event : int
    Data  : SocketEventData }
  with
    static member internal Build(source:C.zmq_event_t) =
      let inline toZMQError num =
        ZMQError(num,Marshal.PtrToStringAnsi(C.zmq_strerror(num)))
      { Event = source.event
        Data  =
          match source.event with
          | ZMQ.EVENT_CONNECTED ->
              Connected(source.connected.addr
                       ,source.connected.fd)
          | ZMQ.EVENT_CONNECT_DELAYED ->
              ConnectDelayed(source.connect_delayed.addr
                            ,source.connect_delayed.err |> toZMQError)
          | ZMQ.EVENT_CONNECT_RETRIED ->
              ConnectRetried(source.connect_retried.addr
                            ,source.connect_retried.interval)
          | ZMQ.EVENT_LISTENING ->      
              Listening(source.listening.addr
                       ,source.listening.fd)
          | ZMQ.EVENT_BIND_FAILED ->    
              BindFailed(source.bind_failed.addr
                        ,source.bind_failed.err |> toZMQError)
          | ZMQ.EVENT_ACCEPTED ->
              Accepted(source.accepted.addr
                      ,source.accepted.fd)
          | ZMQ.EVENT_ACCEPT_FAILED ->
              AcceptFailed(source.accept_failed.addr
                          ,source.accept_failed.err |> toZMQError)
          | ZMQ.EVENT_CLOSED ->
              Closed(source.closed.addr,source.closed.fd)
          | ZMQ.EVENT_CLOSE_FAILED ->   
              CloseFailed(source.close_failed.addr
                         ,source.close_failed.err |> toZMQError)
          | ZMQ.EVENT_DISCONNECTED ->
              Disconnected(source.disconnected.addr
                          ,source.disconnected.fd)
          | _ -> UnknownEvent }
and SocketEventData =
  | Connected       of address * fd
  | ConnectDelayed  of address * ZMQError
  | ConnectRetried  of address * interval
  | Listening       of address * fd
  | BindFailed      of address * ZMQError
  | Accepted        of address * fd
  | AcceptFailed    of address * ZMQError
  | Closed          of address * fd
  | CloseFailed     of address * ZMQError
  | Disconnected    of address * fd
  | UnknownEvent
and address   = string
and fd        = nativeint
and interval  = int

/// Contains methods for working with Socket instances
[<Extension;
  CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Socket =

(* connectivity *)

  /// Causes an endpoint to start accepting
  /// connections at the given address
  [<Extension;CompiledName("Bind")>]
  let bind (socket:Socket) address =
    let okay = C.zmq_bind(!!socket,address)
    if  okay <> 0 then ZMQ.error()

  /// Causes an endpoint to stop accepting
  /// connections at the given address
  [<Extension;CompiledName("Unbind")>]
  let unbind (socket:Socket) address =
    let okay = C.zmq_unbind(!!socket,address)
    if  okay <> 0 then ZMQ.error()

  /// Connects to an endpoint to the given address
  [<Extension;CompiledName("Connect")>]
  let connect (socket:Socket) address =
    let okay = C.zmq_connect(!!socket,address)
    if  okay <> 0 then ZMQ.error()

  /// Disconnects to an endpoint from the given address
  [<Extension;CompiledName("Disconnect")>]
  let disconnect (socket:Socket) address =
    let okay = C.zmq_disconnect(!!socket,address)
    if  okay <> 0 then ZMQ.error()

(* socket options *)

  /// Gets the value of the given option for the given Socket
  [<Extension;CompiledName("GetOption")>]
  let get<'t> (socket:Socket) socketOption : 't =
    let size,read =
      let   t = typeof<'t>
      if    t = typeof<int>     then   4,(snd >> readInt32  >> box)
      elif  t = typeof<bool>    then   4,(snd >> readBool   >> box)
      elif  t = typeof<int64>   then   8,(snd >> readInt64  >> box)
      elif  t = typeof<uint64>  then   8,(snd >> readUInt64 >> box)
      elif  t = typeof<string>  then 255,(       readString >> box)
      elif  t = typeof<byte[]>  then 255,(       readBytes  >> box)
                                else invalidOp "Invalid data type"
    let buffer = Marshal.AllocHGlobal(size)
    try
      let mutable size' = unativeint size
      let okay = C.zmq_getsockopt(!!socket,socketOption,buffer,&size')
      if  okay <> 0 then ZMQ.error()
      downcast read (size',buffer)
    finally
      Marshal.FreeHGlobal(buffer)

  /// Sets the given option value for the given Socket
  [<Extension;CompiledName("SetOption")>]
  let set (socket:Socket) (socketOption,value:'t) =
    let size,write =
      match box value with
      | :? (int32 ) as v  -> sizeof<Int32>,(writeInt32  v)     
      | :? (bool  ) as v  -> sizeof<Int32>,(writeBool   v)   
      | :? (int64 ) as v  -> sizeof<Int32>,(writeInt64  v)    
      | :? (uint64) as v  -> sizeof<Int64>,(writeUInt64 v)   
      | :? (string) as v  -> v.Length     ,(writeString v)
      | :? (byte[]) as v  -> v.Length     ,(writeBytes  v)
      | _                 -> invalidOp "Invalid data type"
    let buffer = Marshal.AllocHGlobal(size)
    try
      write(buffer)
      let okay = C.zmq_setsockopt(!!socket
                                 ,socketOption
                                 ,buffer
                                 ,unativeint size)
      if  okay <> 0 then ZMQ.error()
    finally
      Marshal.FreeHGlobal(buffer)

  /// Sets the given block of option values for the given Socket
  [<Extension;CompiledName("Configure")>]
  let config socket (socketOptions: (int * obj) seq) =
    let set' = set socket in socketOptions |> Seq.iter (set')
  
(* subscripitions *)

  /// Adds one subscription for each of the given topics
  [<Extension;CompiledName("Subscribe")>]
  let subscribe socket topics =
    let setter (t:byte[]) = set socket (ZMQ.SUBSCRIBE,t) |> ignore
    topics |> Seq.iter setter

  /// Removes one subscription for each of the given topics
  [<Extension;CompiledName("Unsubscribe")>]
  let unsubscribe socket topics =
    let setter (t:byte[]) = set socket (ZMQ.UNSUBSCRIBE,t) |> ignore
    topics |> Seq.iter setter

(* message sending *)
  let private (|Okay|Busy|Fail|) = function 
    | -1 -> match C.zmq_errno() with 
            | ZMQ.EAGAIN  -> Busy
            | _           -> Fail
    | _  ->  Okay 

  /// Sends a frame, with the given flags, returning true (or false) 
  /// if the send was successful (or should be re-tried)
  [<Extension;CompiledName("TrySend")>]
  let trySend (socket:Socket) flags frame =
    use frame = new Frame(frame)
    match C.zmq_msg_send(!!frame,!!socket,flags) with
    | Okay -> true
    | Busy -> false
    | Fail -> ZMQ.error()

  let private waitToSend socket flags frame =
    let rec send' ()  =
      match trySend socket flags frame with
      | true  -> ((* okay *))
      | false -> send'()
    send'()

  /// Sends a frame, indicating no more frames will follow
  [<Extension;CompiledName("Send")>]
  let send socket frame = 
    frame |> waitToSend socket ZMQ.WAIT
  
  /// Sends a frame, indicating more frames will follow, 
  /// and returning the given socket
  [<Extension;CompiledName("SendMore")>]
  let sendMore (socket:Socket) frame = 
    frame |> waitToSend socket ZMQ.SNDMORE
    socket
  
  /// Operator equivalent to Socket.send
  let (<<|) socket = send socket
  /// Operator equivalent to Socket.sendMore
  let (<~|) socket = sendMore socket

  /// Operator equivalent to Socket.send (with arguments reversed)
  let (|>>) data socket = socket <<| data
  /// Operator equivalent to Socket.sendMore (with arguments reversed)
  let (|~>) data socket = socket <~| data

  /// Sends all frames of a given message
  [<Extension;CompiledName("SendAll")>]
  let sendAll (socket:Socket) (message:#seq<_>) =
    let last = (message |> Seq.length) - 1
    message 
    |> Seq.mapi (fun i msg -> if i = last then ((|>>) msg) 
                                          else ((|~>) msg) >> ignore)
    |> Seq.iter (fun send' -> socket |> send')

(* message receiving *)

  /// Gets the next available frame from a socket, returning option<frame> 
  /// where None indicates the operation should be re-attempted
  [<Extension;CompiledName("TryRecv")>]
  let tryRecv (socket:Socket) flags =
    use frame = new Frame()
    match C.zmq_msg_recv(!!frame,!!socket,flags) with
    | Okay -> let mutable frame' = Array.empty
              frame' <- frame.Data
              Some(frame')
    | Busy -> None
    | Fail -> ZMQ.error()

  /// Waits for (and returns) the next available frame from a socket
  [<Extension;CompiledName("Recv")>]
  let recv socket = Option.get (tryRecv socket ZMQ.WAIT)
  
  /// Returns true if more message frames are available
  [<Extension;CompiledName("RecvMore")>]
  let recvMore socket = get<bool> socket ZMQ.RCVMORE

  /// Retrieves all frames of the next available message
  [<Extension;CompiledName("RecvAll")>]
  let recvAll socket =
    [|  yield socket |> recv 
        while socket |> recvMore do yield socket |> recv  |]
  
  /// Copies a message frame-wise from one socket to another without
  /// first marshalling the message part into the managed code space
  [<Extension;CompiledName("Transfer")>]
  let transfer (socket:Socket) (target:Socket) =
    use frame = new Frame()
    let rec send' flags =
      match C.zmq_msg_send(!!frame,!!target,flags) with
      | Okay -> ((* pass *))
      | Busy -> send' flags
      | Fail -> ZMQ.error()
    let loop = ref true
    while !loop do
      match C.zmq_msg_recv(!!frame,!!socket,ZMQ.WAIT) with
      | Okay -> loop := socket |> recvMore
                send' (if !loop then ZMQ.SNDMORE else ZMQ.DONTWAIT)
      | _ -> ZMQ.error()

  /// Operator equivalent to Socket.transfer
  let (>|<) socket target = target |> transfer socket

      
(* monitoring *)
  /// Creates a ZMQ.PAIR socket, bound to the given address, which broadcasts
  /// events for the given socket. These events should be consumed by
  /// another ZMQ.PAIR socket, connected to the given address, and
  /// preferrably on a background thread.vis 
  [<Extension;CompiledName("CreateMonitor")>]
  let monitor (socket:Socket) address events =
    let okay = C.zmq_socket_monitor(!!socket,address,events)
    if  okay < 0 then ZMQ.error()

  /// Retreives the next available event from a monitoring socket.
  [<Extension;CompiledName("NextEvent")>]
  let nextEvent (socket:Socket) =
    use frame = new Frame()
    match C.zmq_msg_recv(!!frame,!!socket,ZMQ.WAIT) with
    | Okay -> let ptr = C.zmq_msg_data (!!frame)
              Marshal.PtrToStructure(ptr,typeof<C.zmq_event_t>)
              |> unbox
              |> SocketEvent.Build
              |> Some
    | _ -> None //MAYBE: this could be handled better?
 