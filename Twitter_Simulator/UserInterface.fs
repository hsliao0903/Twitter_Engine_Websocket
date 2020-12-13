module UserInterface
open Message
open System
open Akka.FSharp

(* User Mode Connect/Register check *)
type UserModeStatusCheck =
| Success
| Fail
| Waiting
| Timeout

let mutable (isUserModeLoginSuccess:UserModeStatusCheck) = Waiting

(* User Mode Prompt  *)
let printBanner (printStr:string) =
    printfn "\n----------------------------------"
    printfn "%s" printStr
    printfn "----------------------------------\n"

let showPrompt option = 
    match option with
    | "loginFirst" ->
        printfn "Now you are in \"USER\" mode, you could login as any other existing client or register a new User\n"
        printfn "Please choose one of the commands listed below:"
        printfn "1. register\t register a Twitter account"
        printfn "2. connect\t login as a User"
        printfn "3. exit\t\t terminate this program"
    | "afterLogin" ->
        printfn "\nYou already logged in a Client Termianl\n"
        printfn "Please choose one of the commands listed below:"
        printfn "1. sendtweet\t Post a Tweet for current log in User"
        printfn "2. retweet\t Retweet a Tweet"
        printfn "3. subscribe\t Subscribe to a User"
        printfn "4. disconnect\t Disconnect/log out the current User"
        printfn "5. history\t Query a User's History Tweets"
        printfn "6. tag\t\t Query Tweets with a #Tag"
        printfn "7. mention\t Query Tweets for a mentioned User"
        printfn "8. Qsubscribe\t Query subscribe status for a User"
        printfn "9. exit\t\t terminate this program"
    | _ ->
        ()


let setTimeout _ =
    isUserModeLoginSuccess <- Timeout


let waitForServerResponse (timeout:float) =
    (* timeout: seconds *)
    let timer = new Timers.Timer(timeout*1000.0)
    isUserModeLoginSuccess <- Waiting
    timer.Elapsed.Add(setTimeout)
    timer.Start()
    printBanner "Waiting for server reply..."
    while isUserModeLoginSuccess = Waiting do ()
    timer.Close()

let startUserInterface terminalRef =    

    let mutable curUserID = -1
    let mutable curState= 0
    (* Prompt User for Simulator Usage *)
    
    (showPrompt "loginFirst")
    while true do
        (* First State, User have to register or connect(login) first *)
        (* If successfully registered, *)
        while curState = 0 do
            let inputStr = Console.ReadLine()
            match inputStr with
                | "1" | "register" ->
                    let requestJSON = genRegisterJSON "key"
                    let tmpuserID = getUserID requestJSON
                    terminalRef <! requestJSON
                    printfn "Send register JSON to server...\n%A" requestJSON
                    waitForServerResponse (5.0)
                    if isUserModeLoginSuccess = Success then
                        printBanner ("Successfully registered and login as User"+ tmpuserID.ToString())
                        terminalRef <! """{"ReqType":"UserModeOn", "CurUserID":"""+"\""+ tmpuserID.ToString() + "\"}"
                        curUserID <- tmpuserID
                        curState <- 1
                        (showPrompt "afterLogin")
                    else if isUserModeLoginSuccess = Fail then
                        printBanner ("Faild to register for UserID: " + tmpuserID.ToString())
                        (showPrompt "loginFirst")
                    else
                        printBanner ("Faild to register for UserID: " + tmpuserID.ToString() + "\n(Server no response, timeout occurs)")
                        (showPrompt "loginFirst")

                | "2" | "connect" ->
                    let requestJSON = genConnectDisconnectJSON ("Connect", -1)
                    let tmpuserID = getUserID requestJSON
                    terminalRef <! requestJSON
                    printfn "Send Connect JSON to server...\n%A" requestJSON
                    waitForServerResponse (5.0)
                    if isUserModeLoginSuccess = Success then
                        printBanner ("Successfully connected and login as User"+ tmpuserID.ToString())
                        terminalRef <! """{"ReqType":"UserModeOn", "CurUserID":"""+"\""+ tmpuserID.ToString() + "\"}"
                        curUserID <- tmpuserID
                        curState <- 1
                        (showPrompt "afterLogin")
                    else if isUserModeLoginSuccess = Fail then
                        printBanner ("Faild to connect and login for UserID: " + tmpuserID.ToString())
                        (showPrompt "loginFirst")
                    else
                        printBanner ("Faild to connect and login for UserID: " + tmpuserID.ToString() + "\n(Server no response, timeout occurs)")
                        (showPrompt "loginFirst")

                | "3" | "exit" | "ex" ->
                    printfn "Exit the program, Bye!"
                    Environment.Exit 1
                | _ ->
                    (showPrompt "loginFirst")

        while curState = 1 do
            let inputStr = Console.ReadLine()
            match inputStr with
                | "1"| "sendtweet" ->
                    terminalRef <! genTweetJSON curUserID
                    (showPrompt "afterLogin")
                | "2"| "retweet" -> 
                    terminalRef <! genRetweetJSON curUserID
                    (showPrompt "afterLogin")
                | "3"| "subscribe" | "sub" -> 
                    terminalRef <! genSubscribeJSON curUserID
                    (showPrompt "afterLogin")
                | "4" | "disconnect" ->
                    terminalRef <! genConnectDisconnectJSON ("Disconnect", curUserID)
                    waitForServerResponse (5.0)
                    if isUserModeLoginSuccess = Success then
                        printBanner ("Successfully diconnected and logout User"+ curUserID.ToString())
                        curUserID <- -1
                        curState <- 0
                        (showPrompt "loginFirst")
                    else
                        printBanner ("Faild to disconnect and logout for UserID: " + curUserID.ToString() + "\n(Server no response, timeout occurs)")
                        (showPrompt "afterLogin")
                | "5"| "history" -> 
                    terminalRef <! genQueryJSON "QueryHistory"
                    (showPrompt "afterLogin")
                | "6"| "tag" -> 
                    terminalRef <! genQueryJSON "QueryTag"
                    (showPrompt "afterLogin")
                | "7"| "mention" | "men" -> 
                    terminalRef <! genQueryJSON "QueryMention"
                    (showPrompt "afterLogin")
                | "8"| "Qsubscribe" | "Qsub" -> 
                    terminalRef <! genQueryJSON "QuerySubscribe"
                    (showPrompt "afterLogin")
                | "9" | "exit" | "ex" ->
                    printfn "Exit the program, Bye!"
                    Environment.Exit 1
                | _ ->
                    (showPrompt "afterLogin")