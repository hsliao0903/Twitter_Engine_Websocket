------------------------------------------------------------------------
 Goal
------------------------------------------------------------------------
Implement Websocket and public key based authentication method based on Twitter_Engine_Simulation Project:
https://github.com/hsliao0903/Twitter_Engine_Simulation

Instead of using Akaa.Remote, we use Websocket and JSON for communication
An ECDH authentication method while Twitter accound registraton and API request
A CLI based terminal for accessing the server


------------------------------------------------------------------------
  Libraries/Programming labguage
------------------------------------------------------------------------
.NET 5.0
F#
Akka.FSharp
FSharp.JSON
Websocket-sharp


------------------------------------------------------------------------
  Brief Demo Vedio
------------------------------------------------------------------------
Brief Demo Video on YouTube: 
https://www.youtube.com/watch?v=2qXyVVVXmpU&ab_channel=AlexLiao


------------------------------------------------------------------------
  Author
------------------------------------------------------------------------
  Please let me know if there is any other questions, thanks!
  Hsiang-Yuan Liao, UFID: 4353-5341   hs.liao@ufl.edu
  Tung-Lin Chiang, UFID: 9616-8929 


------------------------------------------------------------------------
  Usage
------------------------------------------------------------------------

* Directories:
    * "Twitter_Engine" -> Server Program
    * "Twitter_Simulator" -> Client Program 

* Special Libraries:
    * FSharp.Json
    * Websocket-sharp
    * Akka.FSharp
    * FSharp.Data

* Description:  
    * Implementation a Websocket interface to [Project 4 part 1] 
    * It contains a Server and a Client program which simulates some Twitter APIs
    * Bonus: public key based authentication method


* Usage: The only difference between user mode and debug mode is the debug log messages
	 Please feel free to choose any mode for each program they don't have to be in the same mode 

    * Client Program in Twitter_Simulator dir
        * “dotnet run user”           (user mode)
        * “dotnet run debug”          (debug mode)

    * Server Program in Twitter_Engine dir
        * “dotnet run”        	      (user mode)
        * “dotnet run debug”          (debug mode)


* Client Program: CLI based terminal (with message prompts, just follow the messages in the terminal)
* Server Program: Press any key to terminate the program while running


------------------------------------------------------------------------
  More Details
------------------------------------------------------------------------

* Limitations:

    * Only support one successful registration in each client program for now (due to the private key issue)
      Able to run multiple client programs in different terminals at the same time

    * The websocket session idle timeout period is small (for debugging and developing purpose)
      If the session timeout happens, please disconnect and then connect the userID again

    * User cannot retweet his own Tweet

    * Only support at most one tag and one mention in each Tweet


* Possible Authentication Method Failure reasons:

    * The authentication method does not complete in one second limit. Server will only cache the challenge for one second.

    * HMAC authentication fails because connecting to different users in a single client program

