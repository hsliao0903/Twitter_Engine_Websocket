module Simulator

open Message
open System
open SimulatorHelpers
open Akka.FSharp
open System.Diagnostics


(* Simulator parameter variables *)
let mutable numClients = 1000
let mutable maxCycle = 1000
let mutable totalRequest = 2147483647
let hashtags = [|"#abc";"#123"; "#DOSP"; "#Twitter"; "#Akka"; "#Fsharp"|]

let mutable percentActive = 60
let mutable percentSendTweet = 50
let mutable percentRetweet = 20
let mutable percentQueryHistory = 20
let mutable percentQueryByMention = 10
let mutable percentQueryByTag = 10

let mutable numActive = numClients * percentActive / 100
let mutable numSendTweet = numActive * percentSendTweet / 100
let mutable numRetweet = numActive * percentRetweet / 100
let mutable numQueryHistory = numActive * percentQueryHistory / 100
let mutable numQueryByMention = numActive * percentQueryByMention / 100
let mutable numQueryByTag = numActive * percentQueryByTag / 100
    

// prompt user to input customized simulation parameters
let getSimualtionParamFromUser () =
    (* Set to simulation mode *)
    printfn "\n\n[Simulator Mode]\n"
    printfn "Please enter some simulation parameters below:"

    printf "How many USERs you would like to simulate?\n"
    numClients <- getUserInput "int" |> int

    printf "How many percent(%%) of USERs are active in each repeated cycle?\n "
    percentActive <- getUserInput "int" |> int
    numActive <- numClients * percentActive / 100

    printf "How many percent(%%) of active USERs send tweet in each repeated cycle?\n " 
    percentSendTweet <- getUserInput "int" |> int
    numSendTweet <- numActive * percentSendTweet / 100

    printf "How many percent(%%) of active USERs retweet in each repeated cycle?\n " 
    percentRetweet <- getUserInput "int" |> int
    numRetweet <- numActive * percentRetweet / 100

    let mutable remaining = 100    
    printf "How many percent(%%) of active USERs query history in each repeated cycle? (max: %d)\n " remaining
    percentQueryHistory <- getUserInput "int" |> int
    numQueryHistory <- numActive * percentQueryHistory / 100
    remaining <- remaining - percentQueryHistory

    printf "How many percent(%%) of active USERs query by metnion in each repeated cycle? (max: %d)\n " remaining
    percentQueryByMention<- getUserInput "int" |> int
    numQueryByMention <- numActive * percentQueryByMention / 100
    remaining <- remaining - percentQueryByMention

    printf "How many percent(%%) of active USERs query by tag in each repeated cycle? (max: %d)\n " remaining
    percentQueryByTag<- getUserInput "int" |> int
    numQueryByTag <- numActive * percentQueryByTag / 100
    remaining <- remaining - percentQueryByTag

    printf "What is the total API request to stop the simulator?\n "
    totalRequest <- getUserInput "int" |> int

    printf "What is the maximum number of cycle of repeated simulations?\n "
    maxCycle <- getUserInput "int" |> int



let startSimulation (system) (globalTimer:Stopwatch) (clientActorNode) = 
    // ----------------------------------------------------------
    // Simulator Scenerio
    // * BASIC SETUP
    //   1. spawn clients
    //   2. register all clients
    //   3. randomly subscribe each other (follow the Zipf)
    //   4. assign random number n = (1 .. 5) * (# of subscriptions) 
    //      tweets with randomly selected tag and mentioned 
    //      for each client to send
    //   5. make all clients disconnected
    // * In every 2 second
    //   1. randomly select some clients connected
    //   2. randomly select some clients disconnected
    //   3. randomly select some connected clients send tweets
    //   4. randomly select some connected clients retweet
    //   5.
    // ----------------------------------------------------------
    (* Setup *)

    printfn "\n\n\n\n\n
    -----------------------------Simulation Setup--------------------------------\n
    This is you simulation settings...\n
    Number of Users: %d\n
    Number of total requests: %d\n
    Number of maximum of repeated cycles: %d\n
    Number of active users: %d (%d%%)\n
    Number of active users who send a tweet: %d (%d%%)\n
    Number of active users who send a retweet: %d (%d%%)\n
    Number of active users who query history: %d (%d%%)\n
    Number of active users who query by mention: %d (%d%%)\n
    Number of active users who send by tag: %d (%d%%)\n
    -----------------------------------------------------------------------------"
        numClients totalRequest maxCycle numActive percentActive numSendTweet 
        percentSendTweet numRetweet percentRetweet numQueryHistory percentQueryHistory
        numQueryByMention percentQueryByMention numQueryByTag percentQueryByTag

    printfn "\n\n[Press any key to start the simulation]\n"
    printfn "[Once it starts, the client registration will need few seconds...]\n"
    Console.ReadLine() |> ignore

    (* 1. spawn clients *)
    let myClients = spawnClients system numClients clientActorNode
    //clientSampler myClients 5 |> List.iter(fun client -> printfn "%s" (client.Path.ToString()))
    //clientSampler myClients 5 |> List.iter(fun client -> printfn "%s" (client.Path.ToString()))

    (* 2. register all clients *)
    let numOfSub = getNumOfSub numClients
    
    myClients 
    |> Array.map(fun client -> 
        async{
            register client
        })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
    
    System.Threading.Thread.Sleep (max numClients 3000)
    //Console.ReadLine() |> ignore

    (* 3. randomly subscribe each other (follow the Zipf) *)
    myClients
    |> Array.mapi(fun i client ->
        async {
            let sub = numOfSub.[i]
            let mutable s = Set.empty
            let rand = Random()
            while s.Count < sub do
                let subscriber = rand.Next(numClients-1)
                if (myClients.[subscriber].Path.Name) <> (client.Path.Name) && not (s.Contains(subscriber)) then
                    s <- s.Add(subscriber)
                    subscribeTo (myClients.[subscriber]) client
        })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
    
    (* 4. assign random number n = (1 .. 5) * (# of subscriptions) tweets  for each client to send *)
    myClients
    |> Array.mapi (fun i client ->
        async{
            let rand1 = Random()
            let rand2 = Random(rand1.Next())
            let numTweets = max (rand1.Next(1,5) * numOfSub.[i]) 1
            
            for i in 1 .. numTweets do
                sendTweet client (tagSampler hashtags) (rand2.Next(numClients))
        })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

    (* 5. make all clients disconnected *)
    myClients
    |> Array.map (fun client -> 
        async{
            disconnect client
        })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

    System.Threading.Thread.Sleep (max numClients 2000)
    printfn "\n\n[The simulation setup has done!]"
    printfn "\n[Press any key to start repeated simulation flow]\n\n"
    Console.ReadLine() |> ignore

    printfn "----------------------------- Repeated Simulation ----------------------------"
    printfn " maxCycle: %d" maxCycle
    printfn "------------------------------------------------------------------------------"
    
    globalTimer.Start()
    let timer = new Timers.Timer(1000.)
    let event = Async.AwaitEvent (timer.Elapsed) |> Async.Ignore
    let connections = Array.create (numClients+1) false
    let mutable cycle = 0
    timer.Start()
    while cycle < maxCycle do
        printfn "----------------------------- CYCLE %d ------------------------------------" cycle
        cycle <- cycle + 1
        Async.RunSynchronously event

        (* randomly select some clients connected *)
        let connecting = (getConnectedID connections)
        let numToDisonnect = ((float) connecting.Length) * 0.3 |> int

        //printfn "toDisconnect %A" numToDisonnect

        arraySampler connecting numToDisonnect        
        |> List.map (fun clientID -> 
            async{
                disconnect myClients.[clientID-1]
                connections.[clientID] <- false
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore
                 
        
        (* randomly select some clients connected *)
        let disconnecting = (getDisconnectedID connections)
        let numToConnect = ((float) disconnecting.Length) * 0.31 |> int
        
        arraySampler disconnecting numToConnect
        |> List.map (fun clientID -> 
            async{
                connect myClients.[clientID-1]
                connections.[clientID] <- true
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

        // Console.ReadLine() |> ignore
        // printfn "numActive %d" numActive
        // printfn "length: %d toConnect: %A" toConnect.Length toConnect
        printfn "length:%d Connecting: %A" (getConnectedID connections).Length (getConnectedID connections)
        // Console.ReadLine() |> ignore

        (* randomly select some clients to send a tweet *)
        let rand = Random()
        arraySampler (getConnectedID connections) numSendTweet
        |> List.map (fun clientID -> 
                async{
                    sendTweet myClients.[clientID-1] (tagSampler hashtags) (rand.Next(1, numClients))
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore 
         
        printfn "START RETWEEEEEET------------------------------------"
        (* randomly select some clients to retweet *)
        let rand = Random()
        arraySampler (getConnectedID connections) numRetweet
        |> List.map (fun clientID -> 
                async{
                    retweet myClients.[clientID-1] (rand.Next(1,numClients))
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore 

        printfn "START HISTORYYYYYY------------------------------------"
        (* randomly select some clients to query history*)
        let rand = Random()
        let shuffledConnects = getConnectedID connections |> shuffle rand

        // Console.ReadLine() |> ignore
        // printfn "connected %d" shuffledConnects.Length
        // printfn "num:%d   %A" numQueryHistory shuffledConnects.[0 .. numQueryHistory-1]
        // printfn "%A" shuffledConnects.[numQueryHistory .. (numQueryHistory + numQueryByMention - 1)]
        // printfn "%A" shuffledConnects.[(numQueryHistory + numQueryByMention) .. (numQueryHistory + numQueryByMention + numQueryByTag - 1)]
        // Console.ReadLine() |> ignore

        shuffledConnects.[0 .. numQueryHistory-1]
        |> Array.map (fun clientID -> 
                async{
                    queryHistory myClients.[clientID-1]
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore 
         
        (* randomly select some clients to query by mention*)
        let rand = Random()
        shuffledConnects.[numQueryHistory .. (numQueryHistory + numQueryByMention - 1)]
        |> Array.map (fun clientID -> 
                async{
                    queryByMention myClients.[clientID-1] (rand.Next(1,numClients))
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore 

        (* randomly select some clients to query by tag*)
        shuffledConnects.[(numQueryHistory + numQueryByMention) .. (numQueryHistory + numQueryByMention + numQueryByTag - 1)]
        |> Array.map (fun clientID -> 
                async{
                    queryByTag myClients.[clientID-1] (tagSampler hashtags)
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore 
      


    globalTimer.Stop()
    System.Threading.Thread.Sleep (max numClients 2000)
    printfn "\n\n[%i cycles of simulation has done!]" maxCycle
    printfn "Total time %A" globalTimer.Elapsed
    Console.ReadLine() |> ignore
    printfn "Total time %A" globalTimer.Elapsed
    Environment.Exit 1
        
