# unity-journale-sdk
Journale's Official Unity SDK

## Installation

Add this package to your Unity project via Package Manager (Git URL or local package).

## Configuration

### Creating SessionConfig

1. Go to **Edit → Project Settings → Journale SDK**
2. Click **"Create SessionConfig"** if one doesn't exist
3. Configure your settings:
   - **Project ID**: Your Journale project identifier
   - **API Base URL**: Default is `https://api.journale.ai`
   - **Auth Platform**: Choose Guest or Steam
   - Other settings as needed

The SessionConfig will be created at:
```
Assets/JournaleClient/Resources/SessionConfig.asset
```

This location ensures:
- ✅ The config persists across Unity sessions
- ✅ It's automatically found by `Resources.Load()` at runtime
- ✅ It's organized separately from other project assets
- ✅ It's easy to version control

### Migration from Old Location

If you have an existing SessionConfig in `Assets/Resources/SessionConfig.asset`, you can:
1. Delete the old one from `Assets/Resources/`
2. Create a new one via Project Settings → Journale SDK

The SDK will automatically find configs in any Resources folder, so both locations work.

## Usage

### Initialization

```csharp
using JournaleClient;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    void Start()
    {
        // Option 1: Auto-load from standard location
        // SessionConfig will be loaded from Assets/JournaleClient/Resources/
        var reply = await Journale.ChatToNpcAsync("npc_001", "Hello!");
        
        // Option 2: Explicit initialization (recommended for control)
        SessionConfig config = Resources.Load<SessionConfig>("SessionConfig");
        Journale.Initialize(config);
    }
}
```

### Chat with NPCs

```csharp
using JournaleClient;
using UnityEngine;

public class NpcInteraction : MonoBehaviour
{
    async void TalkToNpc()
    {
        string reply = await Journale.ChatToNpcAsync(
            localId: "npc_guard_001",
            message: "What's your quest?",
            characterDescription: "A gruff town guard",
            characterId: "guard_archetype_01"
        );
        
        Debug.Log($"NPC says: {reply}");
    }
}
```

## Steam Integration (Optional)

The SDK supports **optional** Steam authentication via Steamworks.NET:

**With Steamworks.NET installed:**
1. Install [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) in your project
2. Set **Auth Platform** to **Steam** in SessionConfig (Project Settings → Journale SDK)
3. The SDK will automatically detect Steamworks.NET and use Steam tickets for authentication

**Without Steamworks.NET:**
- The SDK works perfectly fine in **Guest mode** without Steamworks.NET installed
- Simply set **Auth Platform** to **Guest** in SessionConfig
- No compilation errors will occur - Steamworks code is conditionally compiled

### How it works:
- When Steamworks.NET package is detected, the `STEAMWORKS_NET` define is automatically set
- Steam authentication code is compiled only when this define exists
- Without Steamworks.NET, the SDK gracefully falls back to Guest authentication

## Requirements

- Unity 2020.3 or later
- .NET 4.x or .NET Standard 2.1
- **Optional:** Steamworks.NET (for Steam authentication only)

## License

© Journale AI - All rights reserved

```
