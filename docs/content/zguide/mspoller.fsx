﻿(*** do-not-eval-file ***)
(*** hide ***)
#I "../../../bin"

type ENV = System.Environment

let zmqVersion = if ENV.Is64BitProcess then "x64" else "x86"
ENV.CurrentDirectory <- sprintf "%s../../../../bin/zeromq/%s" __SOURCE_DIRECTORY__ zmqVersion

(**
Multi-socket Poller
====================

Reading from multiple sockets. This version uses a uses ZMQ's polling functionality.
*)

#r "fszmq.dll"
open fszmq
open System.Threading

let main () = 
  use context = new Context ()

  // connect to task ventilator
  let receiver = Context.pull context
  Socket.connect receiver "tcp://localhost:5557"
  
  // connect to weather server
  let subscriber = Context.sub context
  Socket.connect subscriber "tcp://localhost:5556"
  Socket.subscribe subscriber [ "10001"B ]
  
  // process messages from both sockets
  while true do
    let items = 
      [ receiver 
        |> Polling.pollIn (fun s -> let msg = Socket.recv s
                                    ((* process task *)))
        subscriber
        |> Polling.pollIn (fun s -> let msg = Socket.recv s
                                    ((* process update *))) ]
    Polling.pollForever items |> ignore
  
  0 // return code

(*** hide ***)    
main ()
