# BetterTogether

BetterTogether is a simple multiplayer library targeting netstandard2.1, .NET 6, 7 and 8. It is heavily inspired by [Playroom](https://joinplayroom.com/) which is pretty good and available for most web frameworks/game engines. While Playroom has a Unity SDK, it only support WebGL builds of Unity (for now at least). This project however should run on any platform that supports Net Standard 2.1, .NET 6, 7 or 8.

## Features

- Simple API
- Support for RPCs
- More to come (hopefully)

## Installation

Get it from [NuGet](https://www.nuget.org/packages/BetterTogether/) or build it yourself.

```bash
dotnet add package BetterTogether
```
For Unity, use this link in the package manager `https://github.com/ZedDevStuff/BetterTogether.git#unity`

For Unity, use this link in the package manager `https://github.com/ZedDevStuff/BetterTogether.git#unity`

## Usage

### Setup

```csharp
using BetterTogether;

// Set the max number of players to 2
BetterServer server = new BetterServer()
    .WithMaxPlayers(2);
    .WithAdminPlayers(true) // Allow the server to have admins
    .Start(9050);

// Connect to the server
BetterClient client = new BetterClient();

// Fired when the client is connected to the server
client.OnConnected += (id, playerList) =>
{
    Console.WriteLine($"Connected as {id}");
    Console.WriteLine("Players:");
    foreach (var player in playerList)
    {
        Console.WriteLine(player);
    }
};

// Fired when another player is connected
client.OnPlayerConnected += (player) =>
{
    Console.WriteLine($"{player} connected");
};
client.Connect("127.0.0.1", 9050);
```

### State

Objects need to be MemoryPackable. See [MemoryPack](https://github.com/Cysharp/MemoryPack?tab=readme-ov-file#built-in-supported-types)

```csharp
// SetPlayerState is used to set a player state. Only the player or the server can modify this state
client.SetPlayerState<string>("Username", "Player1");
// SetState is used to set a global state. Every player can modify this state
client.SetState<string>("foo", "bar");
// Returns the latest state of the key available on this client
client.GetState<string>("foo");
foreach(var player in client.Players)
{
    Console.WriteLine(player, client.GetPlayerState<string>(player, "Username"));
}
```

### RPCs

```csharp
// Register a RPC
client.RegisterRPC("Hello", (args) =>
{
    string message = MessagePackSerializer.Deserialize<string>(args);
    Console.WriteLine(message);
});

// Call a RPC
client.RPC("Hello", "Hello, World!");
```

### Packets

BetterTogether uses a simple Packet struct to transfer data. If you want to do something on the server with them, you can do so like this

```csharp
public Packet? HandleData(NetPeer peer, Packet packet)
{
    // Do something with the packet or return null
    // Returning null will discard the packet
    return packet;
}

server.DataReceived = HandleData;
```

### Unity support

Unity behaves differently, especially when it comes to threading. To use BetterTogether in Unity, you need to use some kind of dispatcher like [this one](https://github.com/PimDeWitte/UnityMainThreadDispatcher/blob/master/Runtime/UnityMainThreadDispatcher.cs) inside event handlers.
Here is an example using the aforementioned dispatcher

```csharp
using PimDeWitte.UnityMainThreadDispatcher;

// Usual setup ignored

client.OnConnected += (id, playerList) =>
{
	UnityMainThreadDispatcher.Instance().Enqueue(() =>
	{
		Debug.Log($"Connected as {id}");
		Debug.Log("Players:");
		foreach (var player in playerList)
		{
			Debug.Log(player);
		}
	});
};
```
