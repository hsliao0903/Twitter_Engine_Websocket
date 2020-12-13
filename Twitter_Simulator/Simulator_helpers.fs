module SimulatorHelpers

open Message
open Akka.Actor
open Akka.FSharp
open System
open FSharp.Json



// ----------------------------------------------------------
// Clients Manipulation
// ----------------------------------------------------------
let register (client: IActorRef) = 
    client <! """{"ReqType":"Register"}"""
let sendTweet (client: IActorRef) (hashtag: string) (mention: int)= 
    let (request: TweetInfo) = {
        ReqType = "SendTweet";
        UserID = (int) client.Path.Name;
        TweetID = "";
        Time = DateTime.Now;
        Content = "Tweeeeeet";
        Tag = hashtag;
        Mention = mention;
        RetweetTimes = 0 ;
    }
    client <! (Json.serialize request)
let subscribeTo (client: IActorRef) (publisher: IActorRef) = 
    let (request: SubInfo) = {
        ReqType = "Subscribe";
        UserID = (int) client.Path.Name;
        PublisherID = (int) publisher.Path.Name;
    }
    client <! (Json.serialize request)
let retweet (client: IActorRef) (targetUserID: int)=
    let (request: RetweetInfo) = {
        ReqType = "Retweet";
        RetweetID = "";
        TargetUserID = targetUserID;
        UserID = (int) client.Path.Name;
    }
    client <! (Json.serialize request)
let connect (client: IActorRef) = 
    let (request: ConnectInfo) = {
        ReqType = "Connect";
        UserID = client.Path.Name |> int;
    }
    client <! (Json.serialize request)
let disconnect (client: IActorRef) = 
    let (request: ConnectInfo) = {
        ReqType = "Disconnect";
        UserID = client.Path.Name |> int;
    }
    client <! (Json.serialize request)

let queryHistory (client: IActorRef) = 
    let (request: QueryInfo) = {
        ReqType = "QueryHistory";
        UserID = client.Path.Name |> int;
        Tag = "";
    }
    client <! (Json.serialize request)
let queryByMention (client: IActorRef) (mentionedUserID: int) = 
    let (request: QueryInfo) = {
        ReqType = "QueryHistory";
        Tag = "";
        UserID = mentionedUserID;
    }
    client <! (Json.serialize request)
let queryByTag (client: IActorRef) (tag: string)= 
    let (request: QueryInfo) = {
        ReqType = "QueryTag";
        Tag = tag;
        UserID = 0;
    }
    client <! (Json.serialize request)
let queryBySubscribtion (client: IActorRef) (id: int) = 
    let (request: QueryInfo) = {
            ReqType = "QuerySubscribe";
            Tag = "";
            UserID = id;
        }
    client <! (Json.serialize request)
// ----------------------------------------------------------
// Simulator Functions
// | spawnClients
// | arraySampler
// | shuffleList
// | getNumOfSub : Assign random popularity (Zipf) to each acotr
// | tagSampler
// | getConnectedID
// | getDisconnectedID
// ----------------------------------------------------------

let spawnClients (system) (clientNum: int) clientActorNode = 
    [1 .. clientNum]
    |> List.map (fun id -> spawn system ((string) id) clientActorNode)
    |> List.toArray

let arraySampler (arr: 'T []) (num: int) = 
    if arr.Length = 0 then 
        List.empty
    else
        let rnd = System.Random()    
        Seq.initInfinite (fun _ -> rnd.Next (arr.Length)) 
        |> Seq.distinct
        |> Seq.take(num)
        |> Seq.map (fun i -> arr.[i]) 
        |> Seq.toList

let shuffle (rand: Random) (l) = 
    l |> Array.sortBy (fun _ -> rand.Next()) 

let getNumOfSub (numClients: int)= 
    let constant = List.fold (fun acc i -> acc + (1.0/i)) 0.0 [1.0 .. (float) numClients]
    let res =
        [1.0 .. (float) numClients] 
        |> List.map (fun x -> (float) numClients/(x*constant) |> Math.Round |> int)
        |> List.toArray
    //printfn "res\n%A" res
    //Environment.Exit 1
    shuffle (Random()) res             

let tagSampler (hashtags: string []) = 
    let random = Random()
    let rand () = random.Next(hashtags.Length-1)
    hashtags.[rand()]

let getConnectedID (connections: bool []) =
    [1 .. connections.Length-1]
    |> List.filter (fun i -> connections.[i])
    |> List.toArray

let getDisconnectedID (connections: bool []) =
    [1 .. connections.Length-1]
    |> List.filter (fun i -> not connections.[i])
    |> List.toArray

