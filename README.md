# Periscope SDK for Unity

This SDK allows developers to integrate Periscope APIs with their Unity
applications easily. The SDK provides an authentication flow so users can login
with their Periscope account, start / stop broadcast, Go Live and most
importantly start interacting with their audience in a new format. If you
use this SDK in your application, viewers on Periscope can interact with your
application, get immediate feedback and be a part of the experience!

Imagine a VR game where you're no longer alone, you have other people present
in the virtual world potentially helping you, or playing the game alongside you.
For inspiration please check
[this](https://www.periscope.tv/DisneyPixar/1yoKMBEByEdGQ) out!

## PeriscopeAPIManager Prefab

This prefab is where all the API calls are handles. It lives as a singleton and
it is not destroyed during scene changes. This keeps the current state with
respect to all Periscope functionality.

Before you can start making Periscope API calls, you must register your app and
acquire a client id. This allows users to know which app they are giving
permissions to during authentication. To register your app refer to the
[Acquiring a Client ID](#acquiring-a-client-id) section.

The prefab is pretty much configured to be drag and drop without too much
configuration. The only parameter that you should pay attention to as the
developer is the `Min Acceptable Fps`. The ideal value for this parameter
changes based on the type of application you're developing. As a rule of thumb,
we found that for VR games value of `75`, for desktop games `50` and non-fps
intensive applications `20` works well. The details of what this parameter does
can be found in the [FPS-based Parameter Tuning](#fps-based-parameter-tuning).

### Acquiring a Client ID

Go to [Periscope Developer Hub](https://www.periscope.tv/account/developer)
and add click on "Add Application" to add your application to the registry.

### FPS-based Parameter Tuning

Since the popularity of a Periscope broadcast is not known upfront we had to
create a method to throttle incoming / outgoing events (Hearts/Chats/Joins/DMs).
We decided that the frame rate is a good metric to look at to decide if the
Pericope SDK is consuming too much resources, hence degrading the application
performance. This way, Periscope SDK processes events only when the application
can maintain a desired level of frame rate, set by the `Min Acceptable Fps` on
`PeriscopeAPIManager`. You can see the stats on `PeriscopeAPIManager` and
how much throttling is happening and/or how many events are successfully
processed when your application is running in the Unity Editor.

## PeriscopeUserGroupsManager Prefab (optional)

We noticed that a typical use case for the Periscope events is dividing the
audience into groups and have them contribute to the application as part of a
team. For this purpose we created the `PeriscopeUserGroupsManager`. This prefab
integrates with the `PeriscopeAPIManager` and consumes individual heart events
from the audience to accumulate them into an `AggregatedHeartsEvent`. This
prefab tries to maintain an even split between groups, however this is noticed
guaranteed. The group assignment per Periscope viewer is kept constant through
a broadcast, but changes between different broadcasts. This ensures the same
person can leave the broadcast and come back, and remain as part of the same
team. This avoids confusion from the viewer's perspective. On the flip side, the
team assignment may change in between broadcasts, which ensures the same viewer
is not always part of the same group for all broadcasts, which could get boring
very fast.

This prefab also conveniently defines some common DM (Direct Message) templates
that allows the application to communicate and give instructions to the viewers.
These DMs are sent when viewer joins a group, has not been active for a period
of time, when they are expected to be placed in a group but the group is full,
when they send hearts or chats, or simply just periodically.

This prefab is used both for the [snails](#snails) and [bars](#bars) examples.
Please check them out for a better understanding of how it is utilized.

## Developer's Guide

### API

#### 1. Authentication

First, you need to call `Authenticate()` on `PeriscopeAPIManager`. This call
will return immediately to unblock the application. You should listen to the
`AuthenticationStatus` and `AuthUrl` on `PeriscopeAPIManager` and wait until
`AuthUrl` is a valid string and `AuthenticationStatus` is `Waiting`. This is
when you should instruct your user to open the url in a browser and enter the
code from `AuthCode`.

The `AuthenticationStatus` will convert to `Authenticated` once the user
successfully logs into their Periscope account and enters the code to give
permission to your application. At this point your application can create, start
and end broadcasts on behalf of your user.

#### 2. Creating / Starting / Ending a Broadcast

##### Creating a Broadcast

From the API perspective there's a difference between *creating* and *starting*
a broadcast. When the broadcast is created, the API returns a stream url and key
for user to send their video data. At this point the user is not live, until
the broadcast is *started*.

To get the stream info, first call the `GetStreamInfo()` on
`PeriscopeAPIManager`. This call will also return immediately to unblock the
application. At this point you should listen to the `BroadcastStatus`. When the
broadcast creation is successful, `BroadcastStatus` will become `ReadyToStream`,
and `StreamUrl` and `StreamKey` fields will become valid. Present the values of
`StreamUrl` and `StreamKey` to the user and instruct them to start sending their
video data to this RTMP url. The users will have to use one of third party
software that is commonly used for streaming games (e.g.
[OBS Studio](https://obsproject.com/download)).

##### Starting a Broadcast

At this point, user is ready to start the broadcast and *Go Live*. Simply call
the `GoLive()` on `PeriscopeAPIManager` with the desired optional parameters.
This method will return immediately as well. When the user has successfully
started the broadcast and is live, the `BroadcastStatus` will change to `Live`.

In your development if you were able to come this far, now you are live and you
should start having some viewers in your broadcast. Congratulations!! Now you're
ready to have your audience be a part of your application and give them a
voice and purpose.

##### Stopping a Broadcast

Stopping a broadcast is as simple as calling `EndBroadcast()` on
`PeriscopeAPIManager`. Mind that this is a valid action only when the
`BroadcastStatus` is `Live`. Otherwise the call results in a no-op.

**NOTE**: For an example implementation of the auth flow as well as broadcast
start / end controls, you can check the `PeriscopeMenu` component in
[snails](#snails) and [bars](#bars) examples.

#### 3. Connecting and Consuming User Engagement Events

It's great that you've come this far because this is where the magic happens.
You can now have the Periscope audience participate in your application
directly.

To start consuming user events, first you need to connect to the live broadcast
by calling `Connect()` on `PeriscopeAPIManager`. This call will return
immediately as well. Once the call is successful the `ConnectionStatus` will
change to `Connected`. At this point `PeriscopeAPIManager` will start consuming
user engagement events.

`PeriscopeAPIManager` looks for all the implementations of `EventsProcessor`
that are present in the scene and calls the appropriate functions based on the
incoming events. `EventsProcessor` is an abstract class that has three
functions:

* `OnPeriscopeChatEvent(User user, string color, string message)`
* `OnPeriscopeHeartEvent(User user, string color)`
* `OnPeriscopeJoinEvent(User user, string color)`

Responding to these events is as simple as implementing a component that
inherits from `EventsProcessor` and placing it in the scene. In fact
`PeriscopeUserGroupsManager` works the same way, and can be used as a sample
implementation.

**NOTE**: You can listen to the `Error` field on `PeriscopeAPIManager` to keeps
and eye on what's wrong. Especially during the development of your application
this can come in handy. If you encounter an error that you're unable to
interpret, please feel free to contact us.

### Performance Testing

During the development of your application you are unlikely to attract a large
number of viewers. However if your users are popular, or your application gains
popularity once it's released, you will want to be prepared to handle a large
number of incoming / outgoing user events.

For this purpose we are also including a **node.js** script that may help you
with performance testing. The `local_chat_server.js` script acts as the
Periscope server and simulates a large number of viewers sending a large number
of various user events.

1. [Install Node.js](https://nodejs.org/en/download/package-manager)

2. Install websocket via [npm](https://www.npmjs.com/) - `npm install websocket`

3. Run local chat server - `node local_chat_server.js`

4. Enable `Use Localhost` under `PeriscopeAPIManager` prefab and run your scene

## Example Scenes

Finally we are including a few example scenes to showcase what is possible with
this SDK. Feel free to modify them, build your own applications based on them,
or simply Go Live with them.

The example scenes are located at `sdk/Assets/Examples/Scenes/`.

### snails

This is a simple snail racing game. It features two beautiful snails, Gary and
Liz. The rules are simple, viewers are split into two teams, each team is
assigned to a snail and each heart event gives the corresponding team snail.
The more hearts, the faster the snails go. But there's a caveat, when the night
comes, the snails go backwards!

### bars

This is just a simpler version of the snails game that does not have much UI.
This is intended to serve as a barebones application that demonstrate how
`PeriscopeAPIManager`, `PeriscopeUserGroupsManager` and `PeriscopeMenu` come
together. This is a good starting point to inspect code and understand the
SDK.

The viewers are again split into two teams in this scene, each team controlling
a bar. The bars increase or decrease based on the number of hearts viewers
send. The increase / decrease behavior is toggled every 3 seconds.
