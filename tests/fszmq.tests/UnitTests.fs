﻿(* ------------------------------------------------------------------------
This file is part of fszmq.

fszmq is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published 
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

fszmq is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with fszmq. If not, see <http://www.gnu.org/licenses/>.

Copyright (c) 2011-2013 Paulmichael Blasucci
------------------------------------------------------------------------ *)
#if INTERACTIVE
open System
#I "../../packages/FsUnit.1.2.1.0/Lib/Net40"
#I "../../packages/NUnit.2.6.3/lib"
#I "./bin/Debug"
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__ + "/bin/Debug"
printfn "CurrentDirectory = %s" Environment.CurrentDirectory
#r "FsUnit.NUnit.dll"
#r "fszmq.dll"
#r "nunit.framework.dll"
#else
namespace fszmq.tests
#endif

open System
open FsUnit
open fszmq
open NUnit.Framework

[<AutoOpen;TestFixture>]
module UnitTest = 

  [<Test;Category("Miscellany")>]
  let ``libzmq version should be at least 4``() =
    let vsn = ZMQ.version
    printfn "%A" vsn
    match vsn with
    | Version(major,minor,build) -> major |> should equal 4
                                    minor |> should be (greaterThanOrEqualTo 0)
                                    build |> should be (greaterThanOrEqualTo 0)
    //TODO: find a better way to handle failure
    | otherwise -> otherwise |> should not' (equal Unknown)
    
  [<Test;Category("Miscellany")>]
  let ``recv throws TimeoutException if RCVTIMEO expires`` () =
    let testFn () =
      use ctx = new Context ()
      let sck = Context.pair ctx
      Socket.setOption sck (ZMQ.RCVTIMEO,10)
      Socket.bind sck "inproc://dummy"
      sck 
      |> Socket.recv 
      |> printfn "msg: %A"

    let error = Assert.Throws<TimeoutException> (TestDelegate(testFn))
    error.Message |> should equal "The operation has timed out."

(* ZCURVE & Z85 Tests *)
  let BINARY = [| 0x86uy; 0x4Fuy; 0xD2uy; 0x6Fuy; 0xB5uy; 0x59uy; 0xF7uy; 0x5Buy |]
  let STRING = "HelloWorld"

  let inline isIn items v = Seq.exists ((=) v) items

  [<Test;Category("ZCURVE")>]
  let ``keypair generation requires sodium`` () =
    let error = Assert.Throws<ZMQError> (fun () -> Curve.curveKeyPair() |> ignore)
    error.Message |> should equal "Not supported"
    
  //TODO: write passing tests, once you figure out libsodium installation

  [<Test;Category("Z85")>]
  let ``can encode (binary-to-string)`` () =
    let encoded = Z85.encode(BINARY)
    encoded |> should equal STRING
    
  [<Test;Category("Z85")>]
  let ``unencoded binary must be divisible by 4`` () =
    let binary = BINARY.[1 ..] // 7 bytes shouldn't be divisible by 4
    let error  = Assert.Throws<ZMQError> (fun () -> Z85.encode(binary) |> ignore)
    error.Message |> should equal "Invalid argument"

  [<Test;Category("Z85")>]
  let ``unencoded binary must not be zero-length`` () =
    let error = Assert.Throws<ZMQError> (fun () -> Z85.encode([||]) |> ignore)
    error.Message |> should equal "Invalid argument"

  [<Test;Category("Z85")>]
  let ``can decode (string-to-binary)`` () =
    let decoded = Z85.decode(STRING)
    decoded |> should equal BINARY
      
  [<Test;Category("Z85")>]
  let ``encoded string must be divisible by 5`` () =
    let string = STRING + "!" // 11 bytes shouldn't be divisible by 5
    let error  = Assert.Throws<ZMQError> (fun () -> Z85.decode(string) |> ignore)
    error.Message |> should equal "Invalid argument"

  [<Test;Category("Z85")>]
  let ``encoded string must not be zero-length`` () =
    let error = Assert.Throws<ZMQError> (fun () -> Z85.decode("") |> ignore)
    error.Message |> should equal "Invalid argument"
