# Changelog

## 0.9.0

- Added initial state support to `BetterClient`
- Code cleanup and minor improvements

## 0.8.2

- Added `Packet.New<T>` method for creating packets with an object directly instead of `byte[]`. It is generic because MemoryPack can't serialize `System.Object`
- Various small changes

## 0.8.1

- Fixed `BetterClient` not canceling the polling thread when `Disconnect` is called

## 0.8.0

- Added methods for deleting state on the server

## 0.7.0

- Changed Start/Connect methods on `BetterServer` and `BetterClient` to return a bool like the underlying `NetManager` class
- Further improvements to RPCs along with new action types for RPCs

## 0.6.0

- Added `On(string key, Action<Packet> action)` and `Off(string key)` methods to `BetterClient`
- Reworked RPCs and added Server RPCs

## 0.5.2

Added Fluent API to `BetterServer` and `BetterClient`

## 0.5.1

- Banned users now get rejected when trying to connect to the server

## 0.5.0

- `BetterServer` now uses the builder pattern for configuration
- Added kicking and IP banning to server
- Added corresesponding events to `BetterClient` and updated `PacketType`

## 0.4.1

- RPCs now respect the delivery mode set by the client after being received by the server
