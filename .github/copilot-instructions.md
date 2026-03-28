# Copilot Instructions — Client Server Sample

## Project Purpose
A bare-bones Unity multiplayer sample used in a class on multiplayer game development.
The goal is clarity and teachability over production features.

## Unity & Tooling
- Unity 6000.3.6f1 (Unity 6)
- Render pipeline: URP
- Networking: Unity Netcode for GameObjects (NGO) — `com.unity.netcode.gameobjects`
- Input: Unity Input System

## Architecture Decisions
- **Single executable** with a **runtime switch** — one build serves as both client and server.
  - Command-line flag `-server` → headless server mode (`NetworkManager.StartServer()`)
  - Default (no flag) → show in-game UI letting the player choose Host or Join
- **Host mode** (`StartHost()`) is client + server together; use it for quick local testing.
- No compile-time `#if SERVER` / `#if CLIENT` defines — keep it simple and readable for students.

## Code Conventions
- All networked scripts inherit from `NetworkBehaviour` (not `MonoBehaviour`).
- Use `IsOwner` (not `IsLocalPlayer`) for ownership checks on `NetworkBehaviour`.
- `IsServer` / `IsClient` for server-authoritative logic.
- Keep `ServerRpc` and `ClientRpc` names suffixed exactly as NGO requires: `...ServerRpc` / `...ClientRpc`.
- Prefer `NetworkVariable<T>` for replicated state over manual RPCs where practical.
- Scripts live in `Assets/Scripts/`. Subdirectories: `Network/`, `Player/`, `UI/` as the project grows.

## Scene Setup
- Main scene: `Assets/Scenes/ClientServer.unity`
- `NetworkManager` is a GameObject in the scene (not spawned at runtime).
- Player prefab is registered in `NetworkManager.NetworkConfig.PlayerPrefab`.

## Current Implementation Status
- [x] NGO package installed
- [x] NetworkManager configured in scene
- [x] Player prefab created with NetworkObject + NetworkTransform (Owner authority) + PlayerController
- [x] GameManager startup + command-line parsing + OnApplicationQuit shutdown
- [x] NetworkManagerUI (Host / Join / IP input)
- [x] PlayerController with basic networked movement

## What NOT to do
- Do not add anti-cheat, server-authoritative movement validation, or production hardening — this is a teaching sample.
- Do not use Relay or Lobby services (Unity Gaming Services) unless explicitly asked.
- Do not add UI polish or visual effects unless asked; keep the focus on networking concepts.
