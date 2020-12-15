module Message


open System
open System.Security.Cryptography
(* For Json Libraries *)
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json


(* Different API request JSON message structures *)

type ReplyInfo = {
    ReqType : string
    Type : string
    Status : string
    Desc : string option
}

type RegReply = {
    ReqType : string
    Type : string
    Status : string
    ServerPublicKey : string
    Desc : string option
}

type RegJson = {
    ReqType : string
    UserID : int
    UserName : string
    PublicKey : string
}

type SignedTweet = {
    UnsignedJson: string
    HMACSignature: string
}

type TweetInfo = {
    ReqType : string
    UserID : int
    TweetID : string
    Time : DateTime
    Content : string
    Tag : string
    Mention : int
    RetweetTimes : int
}

type TweetReply = {
    ReqType : string
    Type : string
    Status : int
    TweetInfo : TweetInfo
}

type SubInfo = {
    ReqType : string
    UserID : int 
    PublisherID : int
}

type SubReply = {
    ReqType : string
    Type : string
    TargetUserID : int
    Subscriber : int[]
    Publisher : int[]
}

type ConnectInfo = {
    ReqType : string
    UserID : int
    Signature: string
}

type ConnectReply = {
    ReqType: string
    Type: string
    Status: string
    Authentication: string
    Desc: string option
}

type QueryInfo = {
    ReqType : string
    UserID : int
    Tag : string
}

type RetweetInfo = {
    ReqType: string
    UserID : int
    TargetUserID : int
    RetweetID : string
}



// ----------------------------------------------------------
// User Mode for JSON request 
// Generate different JSON string for requests
// ----------------------------------------------------------

let getUserInput (option:string) = 
    let mutable keepPrompt = true
    let mutable userInputStr = ""
    match option with
    | "int" ->
        while keepPrompt do
            printf "Enter a number: "
            userInputStr <- Console.ReadLine()
            match (Int32.TryParse(userInputStr)) with
            | (true, _) -> (keepPrompt <- false)
            | (false, _) ->  printfn "[Error] Invalid number"
        userInputStr
    | "string" ->
        while keepPrompt do
            printf "Enter a string: "
            userInputStr <- Console.ReadLine()
            match userInputStr with
            | "" | "\n" | "\r" | "\r\n" | "\0" -> printfn "[Error] Invalid string"
            | _ -> (keepPrompt <- false)
        userInputStr
    | "YesNo" ->
        while keepPrompt do
            printf "Enter yes/no: "
            userInputStr <- Console.ReadLine()
            match userInputStr.ToLower() with
            | "yes" | "y" -> 
                (keepPrompt <- false) 
                userInputStr<-"yes"
            | "no" | "n" ->
                (keepPrompt <- false) 
                userInputStr<-"no"
            | _ -> printfn "[Error] Invalid input"
        userInputStr
    | _ ->
        userInputStr                                
    


let genRegisterJSON (publicKey:string) =
    
    printfn "Pleae enter an unique number for \"UserID\": "
    let userid = (int) (getUserInput "int")
    printfn "Pleae enter a \"Name\": "
    let username = (getUserInput "string")
    let regJSON:RegJson = { 
        ReqType = "Register" ; 
        UserID =  userid ;
        UserName = username ; 
        PublicKey = publicKey;
    }
    Json.serialize regJSON

let genConnectDisconnectJSON (option:string, curUserID:int) = 
    if option = "Connect" then
        printfn "Please enter a number for \"UserID\": "
        let userid = (int) (getUserInput "int")
        let connectJSON:ConnectInfo = {
            ReqType = "Connect" ;
            UserID = userid ;
            Signature = "";
        }
        Json.serialize connectJSON
    else
        let connectJSON:ConnectInfo = {
            ReqType = "Disconnect" ;
            UserID = curUserID ;
            Signature = "";
        }
        Json.serialize connectJSON

let genTweetJSON curUserID = 
    let mutable tag = ""
    let mutable mention = -1
    printfn "Please enter the \"Content\" of your Tweet: "
    let content = (getUserInput "string")
    printfn "Would you like to add a \"Tag\"?"
    if (getUserInput "YesNo") = "yes" then
        printfn "Please enter a \"Tag\" (with #): "
        tag <- (getUserInput "string")
    printfn "Would you like to add a \"Mention\"?"
    if (getUserInput "YesNo") = "yes" then
        printfn "Please enter a \"UserID\" to mention (w/o @): "
        mention <- (int) (getUserInput "int")

    let (tweetJSON:TweetInfo) = {
        ReqType = "SendTweet" ;
        UserID  = curUserID ;
        TweetID = "" ;
        Time = (DateTime.Now) ;
        Content = content ;
        Tag = tag ;
        Mention = mention ;
        RetweetTimes = 0 ;
    }
    Json.serialize tweetJSON

//subscribe
let genSubscribeJSON curUserID = 
    printfn "Please enter a \"UserID\" you would like to subscribe to: "
    let subToUserID = (int) (getUserInput "int")
    let (subJSON:SubInfo) = {
        ReqType = "Subscribe" ;
        UserID = curUserID ;
        PublisherID = subToUserID;
    }
    Json.serialize subJSON
//retweet
let genRetweetJSON curUserID = 
    printfn "Please enter a \"TweetID\" you would like to \"Retweet\": "
    let retweetID = (getUserInput "string")
    let (retweetJSON:RetweetInfo) = {
        ReqType = "Retweet" ;
        UserID  = curUserID ;
        TargetUserID =  -1 ;
        RetweetID = retweetID ;
    }
    Json.serialize retweetJSON
//queryhistory
//querytag
//querymention
//querysubscirbe
let genQueryJSON (option:string) =
    match option with
    | "QueryTag" ->
        printfn "Please enter the \"Tag\" you would like to query (with #): "
        let tag = getUserInput "string"
        let (queryTagJSON:QueryInfo) = {
            ReqType = "QueryTag" ;
            UserID = -1 ;
            Tag = tag ;
        }
        Json.serialize queryTagJSON
    | "QueryHistory" | "QueryMention" | "QuerySubscribe" ->
        printfn "Please enter a \"UserID\" you would like to \"%s\":" option
        let userid = (int) (getUserInput "int")
        let (queryJSON:QueryInfo) = {
            ReqType = option ;
            UserID = userid ;
            Tag = "" ;
        }
        Json.serialize queryJSON
    | _ -> 
        printfn "[Error] genQueryJSON function wrong input"
        Environment.Exit 1
        ""

let getUserID (jsonStr:string) = 
    let jsonMsg = JsonValue.Parse(jsonStr)
    (jsonMsg?UserID.AsInteger())