# Scynapse Client Principals
## Architecture Specification for DOTNExT Fork

**Version**: 1.0  
**Status**: Architecture Design  
**Context**: This document specifies how Scynapse elevates external clients to first-class participants in the actor system, with identity, state, and presence management.  
**Audience**: AI assistants and developers working on DOTNExT  
**Importance**: This is foundational architecture for Scynapse 1.0 — not a future enhancement.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [The Problem: Anonymous Clients](#2-the-problem-anonymous-clients)
3. [The Solution: Clients as Actors](#3-the-solution-clients-as-actors)
4. [Principal Hierarchy](#4-principal-hierarchy)
5. [Core Capabilities](#5-core-capabilities)
6. [Integration with Scynapse Events](#6-integration-with-scynapse-events)
7. [Security Model](#7-security-model)
8. [Platform Services](#8-platform-services)
9. [Configuration Modes](#9-configuration-modes)
10. [Implementation Architecture](#10-implementation-architecture)
11. [Developer Experience](#11-developer-experience)
12. [Differentiation from Orleans](#12-differentiation-from-orleans)
13. [Implementation Phases](#13-implementation-phases)

---

## 1. Executive Summary

**Scynapse Client Principals** transforms external clients from anonymous connection endpoints into first-class participants in the distributed actor system.

**Core concept**: Every client connection is backed by a grain hierarchy:
- **Account** — The persistent identity (user, service account, guest)
- **Session** — A logical session (login session, API key session)
- **Connection** — The physical TCP/WebSocket connection

**What this enables**:
- **Persistent subscriptions**: Event subscriptions survive disconnection
- **Offline mailboxes**: Events queue when client is offline, deliver on reconnect
- **Built-in security**: AuthN/AuthZ integrated into the actor model
- **Presence management**: Know who's online, detect disconnection
- **State continuity**: Client context persists across connections

**Why this matters**: This architecture makes Scynapse a complete platform for distributed applications, not just a grain framework. Clients aren't second-class citizens reaching into the system — they're participants with their own actor identity.

---

## 2. The Problem: Anonymous Clients

### 2.1 How Orleans Sees Clients Today

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    TRADITIONAL ORLEANS CLIENT MODEL                         │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Client App                 Gateway                    Grains               │
│  ──────────                 ───────                    ──────               │
│                                                                             │
│  OrleansClient  ───TCP───►  Silo Gateway  ───────►   PlayerGrain           │
│  (anonymous)               (router)                   ChatGrain             │
│                                                       LobbyGrain            │
│                                                                             │
│  From Orleans' perspective, the client is:                                  │
│  • An anonymous TCP connection                                              │
│  • No inherent identity                                                     │
│  • No persistent state                                                      │
│  • No mailbox for messages                                                  │
│  • Connection ≡ Identity (lose connection = lose everything)                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Consequences

| Problem | Impact |
|---------|--------|
| **No identity** | Security must be bolted on externally (JWT in every call, custom auth grains) |
| **No state** | Client preferences, subscriptions, context must be manually managed |
| **No mailbox** | Events can't be queued for offline clients |
| **Connection = identity** | Reconnection loses all subscriptions and context |
| **No presence** | Can't easily know "who's online" |

### 2.3 Real-World Pain Points

**Event subscriptions lost on disconnect:**
```csharp
// Client subscribes to game events
await using var sub = await game.SubscribeToUpdatesAsync();

// Network blip → TCP reconnects automatically
// But subscription is GONE — client misses events
// Must remember what they subscribed to and resubscribe
```

**Security is external and repetitive:**
```csharp
// Every grain must validate tokens
public async Task DoSomething(string authToken)
{
    var user = await _authService.ValidateAsync(authToken);  // Every. Single. Call.
    if (!user.HasPermission("something"))
        throw new UnauthorizedAccessException();
    
    // Actual work
}
```

**No offline support:**
```csharp
// User closes laptop, important event fires
// Event goes nowhere — user never sees it
// No way to queue it for later delivery
```

---

## 3. The Solution: Clients as Actors

### 3.1 The Core Insight

What if every client connection had a **grain backing it**? Not just using grains, but **being** a grain (or hierarchy of grains)?

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    NEWORLEANS CLIENT PRINCIPALS MODEL                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Client App                 Gateway              Client Principal Grains    │
│  ──────────                 ───────              ───────────────────────    │
│                                                                             │
│  OrleansClient  ───TCP───►  Silo     ◄──────►   IAccountGrain              │
│                            Gateway               │ (persistent identity)    │
│                               │                  │                          │
│                               │                  └─► ISessionGrain          │
│                               │                      │ (logical session)    │
│                               │                      │                      │
│                               └──────────────────────┴─► IConnectionGrain   │
│                                                          (this connection)  │
│                                                                             │
│  Now the client HAS:                                                        │
│  • Identity (AccountGrain knows who they are)                               │
│  • State (subscriptions, preferences persist in grains)                     │
│  • Mailbox (events queue in grains when offline)                            │
│  • Presence (ConnectionGrain tracks online/offline)                         │
│  • Security principal (authorization checks against AccountGrain)           │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Key Properties

| Property | How Client Principals Provides It |
|----------|----------------------------------|
| **Identity** | `IAccountGrain` persists across all sessions/connections |
| **Sessions** | `ISessionGrain` represents a login session (survives reconnects) |
| **Presence** | `IConnectionGrain` bound to actual TCP connection |
| **State** | Grains have state — subscriptions, preferences, queued events |
| **Mailbox** | Grains can queue messages — deliver on reconnect |
| **Security** | Grains can enforce authorization — integrated, not bolted on |

---

## 4. Principal Hierarchy

### 4.1 Three-Level Structure

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PRINCIPAL HIERARCHY                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  IAccountGrain (Persistent)                                                 │
│  ─────────────────────────                                                  │
│  Key: user-id, service-account-id, or guest-{guid}                          │
│  Lifetime: Permanent (or until account deleted)                             │
│  State: Profile, preferences, capabilities, subscription history            │
│  Persistence: Yes (durable storage)                                         │
│        │                                                                    │
│        │ has many                                                           │
│        ▼                                                                    │
│  ISessionGrain (Semi-Persistent)                                            │
│  ──────────────────────────────                                             │
│  Key: session-token or session-{guid}                                       │
│  Lifetime: Until logout, expiry, or revocation                              │
│  State: Active subscriptions, session context, device info                  │
│  Persistence: Yes (survives reconnection)                                   │
│        │                                                                    │
│        │ has many                                                           │
│        ▼                                                                    │
│  IConnectionGrain (Transient)                                               │
│  ────────────────────────────                                               │
│  Key: connection-{guid}                                                     │
│  Lifetime: TCP connection lifetime                                          │
│  State: Connection metadata, pending messages                               │
│  Persistence: No (in-memory only)                                           │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 4.2 Relationships

```csharp
// Account → Sessions
public interface IAccountGrain : IGrainWithStringKey
{
    // Identity
    Task<AccountInfo> GetInfoAsync();
    
    // Session management
    Task<IReadOnlyList<ISessionGrain>> GetActiveSessionsAsync();
    Task<ISessionGrain> CreateSessionAsync(SessionOptions options);
    Task RevokeAllSessionsAsync();
    
    // Capabilities (for authorization)
    Task<IReadOnlySet<string>> GetCapabilitiesAsync();
    Task<bool> HasCapabilityAsync(string capability);
    
    // Preferences (persistent across sessions)
    Task<T> GetPreferenceAsync<T>(string key);
    Task SetPreferenceAsync<T>(string key, T value);
}

// Session → Connections
public interface ISessionGrain : IGrainWithStringKey
{
    // Parent
    Task<IAccountGrain> GetAccountAsync();
    
    // Connection management
    Task<IReadOnlyList<IConnectionGrain>> GetActiveConnectionsAsync();
    Task<IConnectionGrain> BindConnectionAsync(ConnectionInfo info);
    
    // Subscription management (persistent across reconnects)
    Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync();
    Task AddSubscriptionAsync(SubscriptionInfo subscription);
    Task RemoveSubscriptionAsync(string subscriptionId);
    
    // Session state
    Task<bool> IsValidAsync();
    Task InvalidateAsync();
}

// Connection (bound to TCP)
public interface IConnectionGrain : IGrainWithStringKey
{
    // Parent
    Task<ISessionGrain> GetSessionAsync();
    
    // Connection state
    Task<bool> IsConnectedAsync();
    Task<ConnectionInfo> GetInfoAsync();
    
    // Message delivery
    Task SendAsync(Envelope message);
    Task<IReadOnlyList<Envelope>> DrainPendingAsync();
    
    // Lifecycle
    Task BindAsync(/* connection details from gateway */);
    Task UnbindAsync();
}
```

### 4.3 Lifecycle Scenarios

**User logs in from phone:**
```
1. User authenticates → IAccountGrain retrieved/created for user-id
2. New ISessionGrain created for this login session
3. IConnectionGrain created and bound to TCP connection
4. Client now has: Account → Session → Connection
```

**Same user opens laptop (second connection):**
```
1. Same IAccountGrain (same user)
2. Same ISessionGrain (could be same session token via SSO)
3. NEW IConnectionGrain for the new TCP connection
4. Hierarchy: Account → Session → [Connection1, Connection2]
```

**Network blip (reconnection):**
```
1. Old IConnectionGrain detects disconnect, unbinds
2. TCP reconnects
3. New IConnectionGrain created and bound
4. Session and Account unchanged — subscriptions intact!
```

**User logs out:**
```
1. ISessionGrain.InvalidateAsync() called
2. All IConnectionGrains under session unbound
3. IAccountGrain remains (persistent identity)
4. Next login creates fresh Session → Connection
```

---

## 5. Core Capabilities

### 5.1 Persistent Subscriptions

Event subscriptions are stored in the **SessionGrain**, not tied to the connection:

```csharp
// Traditional Orleans: subscription tied to connection
await grain.SubscribeToEventsAsync();  // Lost on disconnect

// Scynapse Client Principals: subscription tied to session
var session = await GetMySessionGrain();
await session.SubscribeToGrainEventAsync<IPlayerGrain>("player-1", "ChatMessage");
// Subscription persists in SessionGrain
// Survives connection drops, reconnects
```

**How it works:**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    PERSISTENT SUBSCRIPTION FLOW                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  SessionGrain                                                               │
│  ────────────                                                               │
│  State:                                                                     │
│    Subscriptions: [                                                         │
│      { GrainType: IPlayerGrain, GrainKey: "player-1", Event: "ChatMessage" }│
│      { GrainType: ILobbyGrain, GrainKey: "main", Event: "PlayerJoined" }    │
│    ]                                                                        │
│                                                                             │
│  On Activation:                                                             │
│    1. Read subscriptions from state                                         │
│    2. Subscribe to each SMS stream                                          │
│    3. When events arrive → forward to active connection(s)                  │
│                                                                             │
│  On Event Received:                                                         │
│    If connection active → SendAsync(event)                                  │
│    If no connection → Queue in mailbox (see 5.2)                            │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Offline Mailbox

When a client is offline, events queue in the grain hierarchy:

```csharp
public partial class SessionGrain : Grain, ISessionGrain
{
    private readonly Queue<Envelope> _mailbox = new();
    private IConnectionGrain? _activeConnection;
    
    // Called when subscribed stream receives event
    internal async Task OnEventReceivedAsync(Envelope envelope)
    {
        if (_activeConnection != null && await _activeConnection.IsConnectedAsync())
        {
            // Deliver immediately
            await _activeConnection.SendAsync(envelope);
        }
        else
        {
            // Queue for later
            _mailbox.Enqueue(envelope);
            
            // Optional: trim if too many (configurable)
            while (_mailbox.Count > MaxMailboxSize)
                _mailbox.Dequeue();
        }
    }
    
    // Called when connection becomes available
    public async Task OnConnectionEstablishedAsync(IConnectionGrain connection)
    {
        _activeConnection = connection;
        
        // Drain mailbox
        while (_mailbox.TryDequeue(out var envelope))
        {
            await connection.SendAsync(envelope);
        }
    }
}
```

**Client experience:**

```csharp
// User is playing a game, subscribes to team events
await session.SubscribeToGrainEventAsync<ITeamGrain>(myTeamId, "AchievementUnlocked");

// User closes laptop (connection drops)
// While offline, team member unlocks achievement
// Event is queued in SessionGrain's mailbox

// User opens laptop, connection re-establishes
// SessionGrain drains mailbox
// User sees: "🏆 TeamMate unlocked 'First Victory'!"
```

### 5.3 Presence Management

The connection grain tracks presence:

```csharp
public interface IPresenceService
{
    // Who's online right now?
    Task<IReadOnlyList<IAccountGrain>> GetOnlineAccountsAsync();
    
    // Is this specific user online?
    Task<bool> IsOnlineAsync(string accountId);
    
    // Get all connections for an account
    Task<IReadOnlyList<IConnectionGrain>> GetConnectionsAsync(string accountId);
    
    // Events
    event EventHandler<AccountPresenceChangedArgs>? PresenceChanged;
}
```

**Implementation via grain observers:**

```csharp
// ConnectionGrain notifies presence service on bind/unbind
public partial class ConnectionGrain : Grain, IConnectionGrain
{
    public async Task BindAsync(ConnectionInfo info)
    {
        _info = info;
        _isBound = true;
        
        // Notify presence service
        var presenceGrain = GrainFactory.GetGrain<IPresenceGrain>(0);
        await presenceGrain.OnConnectionBoundAsync(this.GetPrimaryKeyString(), 
            await GetSessionAsync(),
            await (await GetSessionAsync()).GetAccountAsync());
    }
    
    public async Task UnbindAsync()
    {
        _isBound = false;
        
        var presenceGrain = GrainFactory.GetGrain<IPresenceGrain>(0);
        await presenceGrain.OnConnectionUnboundAsync(this.GetPrimaryKeyString());
    }
}
```

---

## 6. Integration with Scynapse Events

### 6.1 How Events Flow Through Client Principals

With Client Principals, event delivery changes:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    EVENT FLOW WITH CLIENT PRINCIPALS                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  WITHOUT Client Principals (v1 Events):                                     │
│  ─────────────────────────────────────                                      │
│                                                                             │
│  Grain ───► SMS Stream ───► Client Proxy ───► Local Handlers                │
│                  │                                                          │
│                  └── Connection drops? Subscription lost!                   │
│                                                                             │
│  ═══════════════════════════════════════════════════════════════════════    │
│                                                                             │
│  WITH Client Principals:                                                    │
│  ──────────────────────                                                     │
│                                                                             │
│  Grain ───► SMS Stream ───► SessionGrain ───► ConnectionGrain ───► Client   │
│                                   │                                         │
│                                   │ Connection drops?                       │
│                                   │ Events queue in SessionGrain mailbox    │
│                                   │                                         │
│                                   │ Reconnection?                           │
│                                   │ Mailbox drains to new ConnectionGrain   │
│                                   │                                         │
│                                   └── Subscription survives!                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 6.2 Developer Experience (Updated)

With Client Principals, the event subscription API becomes:

```csharp
// Get my session (established during connection setup)
var session = await client.GetMySessionAsync();

// Subscribe to events through the session (persistent)
await session.SubscribeToEventAsync<IPlayerGrain>("player-1", g => g.ChatMessage);

// Attach local handlers (same as before)
var player = client.GetGrain<IPlayerGrain>("player-1");
player.ChatMessage += (s, msg) => Console.WriteLine(msg);

// Subscription is now persistent:
// - Survives disconnection
// - Events queue while offline
// - Automatically redelivered on reconnect
```

**Or, for simpler scenarios, transparent integration:**

```csharp
// Scynapse can automatically route subscriptions through your session
var player = client.GetGrain<IPlayerGrain>("player-1");

// This internally creates a persistent subscription via your SessionGrain
await using var sub = await player.SubscribeToChatMessageAsync();

player.ChatMessage += (s, msg) => Console.WriteLine(msg);

// If Client Principals is enabled, this subscription is persistent
// If not enabled (e.g., development mode), falls back to v1 behavior
```

### 6.3 Configuration Toggle

```csharp
// Enable Client Principals for event routing
siloBuilder.ConfigureScynapse(options =>
{
    options.Events.UsePersistentSubscriptions = true;  // Routes through SessionGrain
    options.Events.OfflineMailboxSize = 1000;          // Queue up to 1000 events
    options.Events.MailboxRetentionPeriod = TimeSpan.FromDays(7);  // Keep for 7 days
});
```

---

## 7. Security Model

### 7.1 The Principal as Security Context

The AccountGrain IS the security principal:

```csharp
public interface IAccountGrain : IGrainWithStringKey
{
    // ═══════════════════════════════════════════════════════════════
    // AUTHENTICATION
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Authenticate with credentials.
    /// </summary>
    Task<AuthResult> AuthenticateAsync(AuthCredentials credentials);
    
    /// <summary>
    /// Authenticate via external provider (OAuth, OIDC).
    /// </summary>
    Task<AuthResult> AuthenticateExternalAsync(ExternalAuthInfo info);
    
    /// <summary>
    /// Check if currently authenticated.
    /// </summary>
    Task<bool> IsAuthenticatedAsync();
    
    /// <summary>
    /// Get authentication claims.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetClaimsAsync();
    
    // ═══════════════════════════════════════════════════════════════
    // AUTHORIZATION
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Check if account has a capability.
    /// </summary>
    Task<bool> HasCapabilityAsync(string capability);
    
    /// <summary>
    /// Check if account can perform operation on grain.
    /// </summary>
    Task<bool> CanAccessAsync(GrainId grainId, string operation);
    
    /// <summary>
    /// Get all capabilities for this account.
    /// </summary>
    Task<IReadOnlySet<string>> GetCapabilitiesAsync();
}
```

### 7.2 Call Context Integration

Every grain call carries the calling principal:

```csharp
public static class CallContext
{
    /// <summary>
    /// Get the AccountGrain of the caller (if authenticated).
    /// </summary>
    public static IAccountGrain? GetCallingAccount()
    {
        return RequestContext.Get("Scynapse.CallingAccount") as IAccountGrain;
    }
    
    /// <summary>
    /// Get the SessionGrain of the caller.
    /// </summary>
    public static ISessionGrain? GetCallingSession()
    {
        return RequestContext.Get("Scynapse.CallingSession") as ISessionGrain;
    }
    
    /// <summary>
    /// Get the ConnectionGrain of the caller (if external client).
    /// </summary>
    public static IConnectionGrain? GetCallingConnection()
    {
        return RequestContext.Get("Scynapse.CallingConnection") as IConnectionGrain;
    }
}
```

### 7.3 Authorization in Grains

```csharp
public class SecureGrain : Grain, ISecureGrain
{
    public async Task<SensitiveData> GetSensitiveDataAsync()
    {
        // Get calling principal
        var caller = CallContext.GetCallingAccount();
        
        if (caller == null)
            throw new AuthenticationException("Authentication required");
        
        if (!await caller.CanAccessAsync(this.GetGrainId(), "read"))
            throw new AuthorizationException("Not authorized to read this grain");
        
        return _sensitiveData;
    }
}
```

### 7.4 Declarative Authorization (Future)

```csharp
// Attribute-based authorization
public class SecureGrain : Grain, ISecureGrain
{
    [RequireCapability("admin")]
    public Task DeleteEverythingAsync()
    {
        // Only accounts with "admin" capability can call this
    }
    
    [RequireAuthenticated]
    public Task<UserData> GetMyDataAsync()
    {
        // Any authenticated account can call this
    }
    
    [AllowAnonymous]
    public Task<PublicInfo> GetPublicInfoAsync()
    {
        // Anyone can call this, even guests
    }
}
```

### 7.5 Guest Accounts

Unauthenticated clients get a guest account:

```csharp
// When client connects without authentication
// System creates: IAccountGrain with key "guest-{connection-guid}"
// Guest accounts have limited capabilities

siloBuilder.ConfigureScynapse(options =>
{
    options.Security.AllowGuests = true;
    options.Security.GuestCapabilities = new[]
    {
        "read:public",        // Can read public data
        "subscribe:public",   // Can subscribe to public events
        // Cannot write, cannot access private grains
    };
});
```

---

## 8. Platform Services

Client Principals enables building platform services as grains:

### 8.1 Authentication Service

```csharp
public interface IAuthenticationService : IGrainWithIntegerKey
{
    /// <summary>
    /// Authenticate credentials and return/create account.
    /// </summary>
    Task<IAccountGrain> AuthenticateAsync(AuthCredentials credentials);
    
    /// <summary>
    /// Create a new account.
    /// </summary>
    Task<IAccountGrain> CreateAccountAsync(CreateAccountRequest request);
    
    /// <summary>
    /// Validate a session token.
    /// </summary>
    Task<ISessionGrain?> ValidateSessionAsync(string token);
}
```

### 8.2 Presence Service

```csharp
public interface IPresenceService : IGrainWithIntegerKey
{
    Task<IReadOnlyList<AccountPresenceInfo>> GetOnlineAccountsAsync();
    Task<IReadOnlyList<AccountPresenceInfo>> GetOnlineInGroupAsync(string groupId);
    Task<bool> IsOnlineAsync(string accountId);
    
    // Subscription to presence changes
    Task<IEventSubscription<AccountPresenceInfo>> SubscribeToPresenceChangesAsync();
}
```

### 8.3 Notification Service

```csharp
public interface INotificationService : IGrainWithIntegerKey
{
    /// <summary>
    /// Send notification to account (queues if offline).
    /// </summary>
    Task SendToAccountAsync(string accountId, Notification notification);
    
    /// <summary>
    /// Send notification to all accounts in a group.
    /// </summary>
    Task SendToGroupAsync(string groupId, Notification notification);
    
    /// <summary>
    /// Broadcast to all connected clients.
    /// </summary>
    Task BroadcastAsync(Notification notification);
}

// Implementation leverages Client Principals
public class NotificationServiceGrain : Grain, INotificationService
{
    public async Task SendToAccountAsync(string accountId, Notification notification)
    {
        var account = GrainFactory.GetGrain<IAccountGrain>(accountId);
        var sessions = await account.GetActiveSessionsAsync();
        
        foreach (var session in sessions)
        {
            // Session handles delivery or queueing
            await session.DeliverNotificationAsync(notification);
        }
    }
}
```

### 8.4 Rate Limiting Service

```csharp
public interface IRateLimitService : IGrainWithIntegerKey
{
    /// <summary>
    /// Check if account is within rate limits.
    /// </summary>
    Task<RateLimitResult> CheckAsync(string accountId, string operation);
    
    /// <summary>
    /// Record an operation for rate limiting.
    /// </summary>
    Task RecordAsync(string accountId, string operation);
}

// Can be integrated into grain calls via filter
public class RateLimitFilter : IIncomingGrainCallFilter
{
    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var caller = CallContext.GetCallingAccount();
        if (caller != null)
        {
            var rateLimit = GrainFactory.GetGrain<IRateLimitService>(0);
            var result = await rateLimit.CheckAsync(
                caller.GetPrimaryKeyString(), 
                context.MethodName);
            
            if (!result.Allowed)
                throw new RateLimitExceededException(result.RetryAfter);
        }
        
        await context.Invoke();
    }
}
```

---

## 9. Configuration Modes

### 9.1 Development Mode (Open)

```csharp
siloBuilder.ConfigureScynapse(options =>
{
    options.Security.Mode = SecurityMode.Open;
    
    // No authentication required
    // No guest accounts (clients are truly anonymous)
    // All grains accessible to all clients
    // Subscriptions are transient (v1 behavior)
});
```

### 9.2 Guest-Allowed Mode

```csharp
siloBuilder.ConfigureScynapse(options =>
{
    options.Security.Mode = SecurityMode.GuestAllowed;
    options.Security.GuestCapabilities = new[]
    {
        "read:public",
        "subscribe:public",
    };
    
    // Unauthenticated clients get guest AccountGrain
    // Guests have limited capabilities
    // Can upgrade to authenticated at any time
});
```

### 9.3 Authenticated-Only Mode

```csharp
siloBuilder.ConfigureScynapse(options =>
{
    options.Security.Mode = SecurityMode.AuthenticatedOnly;
    
    // All clients must authenticate before any grain access
    // Connection refused until authentication succeeds
    // Strongest security posture
});
```

### 9.4 Custom Mode

```csharp
siloBuilder.ConfigureScynapse(options =>
{
    options.Security.Mode = SecurityMode.Custom;
    options.Security.CustomAuthenticator = new MyAuthenticator();
    options.Security.CustomAuthorizer = new MyAuthorizer();
});
```

---

## 10. Implementation Architecture

### 10.1 Gateway Integration

The Orleans gateway is extended to create Client Principal grains:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    GATEWAY INTEGRATION                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Client TCP Connect                                                         │
│        │                                                                    │
│        ▼                                                                    │
│  Gateway.OnConnectionAccepted()                                             │
│        │                                                                    │
│        ├─── Create IConnectionGrain(connection-{guid})                      │
│        │                                                                    │
│        ├─── If has session token:                                           │
│        │        Validate and get existing ISessionGrain                     │
│        │                                                                    │
│        ├─── If no session token:                                            │
│        │        If GuestAllowed: Create guest AccountGrain + SessionGrain   │
│        │        If AuthenticatedOnly: Reject (or wait for auth)             │
│        │                                                                    │
│        └─── Bind ConnectionGrain to SessionGrain                            │
│                                                                             │
│  Now all grain calls from this connection include:                          │
│  - RequestContext["Scynapse.CallingAccount"] = accountGrain               │
│  - RequestContext["Scynapse.CallingSession"] = sessionGrain               │
│  - RequestContext["Scynapse.CallingConnection"] = connectionGrain         │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 10.2 Request Context Flow

```csharp
// Gateway sets up context for each call
public class ClientPrincipalCallFilter : IOutgoingGrainCallFilter
{
    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        // Set principal context (flows through all grain calls)
        RequestContext.Set("Scynapse.CallingAccount", _accountGrain);
        RequestContext.Set("Scynapse.CallingSession", _sessionGrain);
        RequestContext.Set("Scynapse.CallingConnection", _connectionGrain);
        
        await context.Invoke();
    }
}
```

### 10.3 Grain State Storage

```csharp
// AccountGrain uses persistent storage
[StorageProvider(ProviderName = "Scynapse.Accounts")]
public class AccountGrain : Grain<AccountState>, IAccountGrain
{
    // State persists across activations
}

// SessionGrain uses persistent storage
[StorageProvider(ProviderName = "Scynapse.Sessions")]
public class SessionGrain : Grain<SessionState>, ISessionGrain
{
    // State persists across reconnections
}

// ConnectionGrain is transient
public class ConnectionGrain : Grain, IConnectionGrain
{
    // In-memory only, no persistence
}
```

---

## 11. Developer Experience

### 11.1 Client SDK

```csharp
// Scynapse client with principal support
var client = new ScynapseClientBuilder()
    .UseConfiguration(config)
    .Build();

await client.ConnectAsync();

// Get my principal grains
IAccountGrain myAccount = await client.GetMyAccountAsync();
ISessionGrain mySession = await client.GetMySessionAsync();

// Access grains (calls automatically include principal context)
var player = client.GetGrain<IPlayerGrain>("player-1");
await player.SendChatAsync("Hello!");  // Principal context flows automatically

// Subscribe with persistence
await mySession.SubscribeToEventAsync<IPlayerGrain>("player-1", g => g.ChatMessage);
player.ChatMessage += (s, msg) => Console.WriteLine(msg);
// Subscription survives disconnect!

// Disconnect and reconnect
await client.DisconnectAsync();
// ... time passes, events queue in SessionGrain ...
await client.ReconnectAsync();
// Queued events delivered automatically!
```

### 11.2 Server SDK (Grain Development)

```csharp
public class GameGrain : Grain, IGameGrain
{
    public async Task JoinAsync()
    {
        // Get the calling principal
        var account = CallContext.GetCallingAccount();
        
        if (account == null)
            throw new AuthenticationException("Must be logged in to join");
        
        if (!await account.HasCapabilityAsync("game:join"))
            throw new AuthorizationException("Not authorized to join games");
        
        var accountId = account.GetPrimaryKeyString();
        _players.Add(accountId);
        
        // Notify others
        PlayerJoined?.Invoke(this, accountId);
    }
    
    public event EventHandler<string>? PlayerJoined;
}
```

### 11.3 Authentication Flow

```csharp
// Client authenticates
var client = new ScynapseClientBuilder().Build();
await client.ConnectAsync();

// Initially a guest
var myAccount = await client.GetMyAccountAsync();
Console.WriteLine(await myAccount.IsAuthenticatedAsync());  // false

// Authenticate
var authResult = await myAccount.AuthenticateAsync(new AuthCredentials
{
    Username = "louis",
    Password = "secret"
});

if (authResult.Success)
{
    // Now authenticated — account upgraded from guest
    Console.WriteLine(await myAccount.IsAuthenticatedAsync());  // true
    Console.WriteLine(await myAccount.HasCapabilityAsync("admin"));  // depends on user
}
```

---

## 12. Differentiation from Orleans

### 12.1 Feature Comparison

| Feature | Plain Orleans | Scynapse with Client Principals |
|---------|---------------|-----------------------------------|
| Client identity | Anonymous connection | First-class AccountGrain |
| Session management | None (manual) | Built-in SessionGrain |
| Presence | Manual implementation | Built-in ConnectionGrain + service |
| Event subscriptions | Tied to connection | Persist in SessionGrain |
| Offline support | None | Mailbox queuing in SessionGrain |
| Authentication | External (JWT, etc.) | Integrated in AccountGrain |
| Authorization | External | Integrated, declarative |
| Security context | Manual propagation | Automatic via RequestContext |
| Reconnection | Subscriptions lost | Subscriptions survive |

### 12.2 What This Enables

**Applications impossible/hard with plain Orleans:**

1. **Chat with offline delivery**: Messages queue for offline users, deliver on reconnect
2. **Multiplayer games with reconnect**: Player rejoins game after network blip, nothing lost
3. **Real-time dashboards**: Subscribe to metrics, survive disconnects, see missed alerts
4. **Collaborative apps**: Presence indicators, notifications, offline sync
5. **Secure multi-tenant systems**: Built-in tenant isolation via capabilities

### 12.3 Migration Path

Existing Orleans applications can adopt Client Principals incrementally:

```csharp
// Phase 1: Enable but don't require
options.Security.Mode = SecurityMode.GuestAllowed;
// All clients work as before, but now have principal grains

// Phase 2: Add authentication
// Gradually add auth to grains that need it
[RequireAuthenticated]
public Task SensitiveOperationAsync() { ... }

// Phase 3: Move to authenticated-only
options.Security.Mode = SecurityMode.AuthenticatedOnly;
```

---

## 13. Implementation Phases

### Phase 1: Core Principal Grains

- [ ] Define `IAccountGrain`, `ISessionGrain`, `IConnectionGrain` interfaces
- [ ] Implement basic AccountGrain (identity, capabilities)
- [ ] Implement basic SessionGrain (lifecycle, connection tracking)
- [ ] Implement basic ConnectionGrain (bind/unbind)
- [ ] Unit tests for grain lifecycle

### Phase 2: Gateway Integration

- [ ] Modify gateway to create ConnectionGrain on accept
- [ ] Implement session token validation
- [ ] Implement guest account creation
- [ ] Set up RequestContext propagation
- [ ] Integration tests for connection flow

### Phase 3: Call Context & Authorization

- [ ] Implement `CallContext` static class
- [ ] Add principal to RequestContext in gateway filter
- [ ] Implement basic authorization checks
- [ ] Add `[RequireCapability]` attribute support
- [ ] Integration tests for authorization

### Phase 4: Persistent Subscriptions

- [ ] Add subscription tracking to SessionGrain
- [ ] Implement subscription persistence
- [ ] Integrate with Scynapse Events
- [ ] Handle subscription restoration on reconnect
- [ ] Integration tests for persistent subscriptions

### Phase 5: Offline Mailbox

- [ ] Add mailbox queue to SessionGrain
- [ ] Implement mailbox drain on reconnect
- [ ] Add mailbox size limits and retention policy
- [ ] Integration tests for offline/online transitions

### Phase 6: Platform Services

- [ ] Implement IAuthenticationService
- [ ] Implement IPresenceService
- [ ] Implement INotificationService
- [ ] Add configuration options
- [ ] Documentation and samples

### Phase 7: Security Hardening

- [ ] Implement rate limiting integration
- [ ] Add audit logging
- [ ] Security review
- [ ] Penetration testing
- [ ] Documentation for security best practices

---

## Appendix A: Interface Definitions

```csharp
namespace Orleans.Principals;

// ═══════════════════════════════════════════════════════════════════════════
// ACCOUNT GRAIN
// ═══════════════════════════════════════════════════════════════════════════

public interface IAccountGrain : IGrainWithStringKey
{
    // Identity
    Task<AccountInfo> GetInfoAsync();
    Task<IReadOnlyDictionary<string, string>> GetClaimsAsync();
    
    // Authentication
    Task<AuthResult> AuthenticateAsync(AuthCredentials credentials);
    Task<AuthResult> AuthenticateExternalAsync(ExternalAuthInfo info);
    Task<bool> IsAuthenticatedAsync();
    Task SignOutAsync();
    
    // Authorization
    Task<bool> HasCapabilityAsync(string capability);
    Task<bool> CanAccessAsync(GrainId grainId, string operation);
    Task<IReadOnlySet<string>> GetCapabilitiesAsync();
    
    // Sessions
    Task<IReadOnlyList<ISessionGrain>> GetActiveSessionsAsync();
    Task<ISessionGrain> CreateSessionAsync(SessionOptions? options = null);
    Task RevokeAllSessionsAsync();
    
    // Preferences
    Task<T?> GetPreferenceAsync<T>(string key);
    Task SetPreferenceAsync<T>(string key, T value);
}

// ═══════════════════════════════════════════════════════════════════════════
// SESSION GRAIN
// ═══════════════════════════════════════════════════════════════════════════

public interface ISessionGrain : IGrainWithStringKey
{
    // Hierarchy
    Task<IAccountGrain> GetAccountAsync();
    
    // Connections
    Task<IReadOnlyList<IConnectionGrain>> GetActiveConnectionsAsync();
    Task<IConnectionGrain> BindConnectionAsync(ConnectionInfo info);
    
    // Lifecycle
    Task<bool> IsValidAsync();
    Task InvalidateAsync();
    Task<DateTime> GetExpiresAtAsync();
    Task ExtendAsync(TimeSpan duration);
    
    // Subscriptions (persistent)
    Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync();
    Task<string> AddSubscriptionAsync(SubscriptionInfo subscription);
    Task RemoveSubscriptionAsync(string subscriptionId);
    
    // Mailbox
    Task<int> GetMailboxCountAsync();
    Task ClearMailboxAsync();
    
    // Internal
    Task DeliverAsync(Envelope envelope);
}

// ═══════════════════════════════════════════════════════════════════════════
// CONNECTION GRAIN
// ═══════════════════════════════════════════════════════════════════════════

public interface IConnectionGrain : IGrainWithStringKey
{
    // Hierarchy
    Task<ISessionGrain> GetSessionAsync();
    
    // State
    Task<bool> IsConnectedAsync();
    Task<ConnectionInfo> GetInfoAsync();
    
    // Lifecycle
    Task BindAsync(ConnectionBindRequest request);
    Task UnbindAsync();
    
    // Messaging
    Task SendAsync(Envelope envelope);
    Task<IReadOnlyList<Envelope>> DrainPendingAsync();
}
```

---

## Appendix B: Configuration Options

```csharp
namespace Orleans.Configuration;

public class ScynapseOptions
{
    public SecurityOptions Security { get; set; } = new();
    public EventOptions Events { get; set; } = new();
    public PrincipalOptions Principals { get; set; } = new();
}

public class SecurityOptions
{
    public SecurityMode Mode { get; set; } = SecurityMode.Open;
    public string[] GuestCapabilities { get; set; } = Array.Empty<string>();
    public IAuthenticator? CustomAuthenticator { get; set; }
    public IAuthorizer? CustomAuthorizer { get; set; }
}

public class EventOptions
{
    public bool UsePersistentSubscriptions { get; set; } = false;
    public int OfflineMailboxSize { get; set; } = 1000;
    public TimeSpan MailboxRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}

public class PrincipalOptions
{
    public TimeSpan DefaultSessionDuration { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan ConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public string AccountStorageProvider { get; set; } = "Scynapse.Accounts";
    public string SessionStorageProvider { get; set; } = "Scynapse.Sessions";
}

public enum SecurityMode
{
    /// <summary>
    /// No authentication, no client principals.
    /// Good for development.
    /// </summary>
    Open,
    
    /// <summary>
    /// Guests allowed with limited capabilities.
    /// Can upgrade to authenticated.
    /// </summary>
    GuestAllowed,
    
    /// <summary>
    /// All clients must authenticate.
    /// No guest access.
    /// </summary>
    AuthenticatedOnly,
    
    /// <summary>
    /// Custom authentication/authorization.
    /// </summary>
    Custom
}
```

---

## Appendix C: Call Context API

```csharp
namespace Orleans.Principals;

/// <summary>
/// Access the calling client's principal grains from within any grain.
/// </summary>
public static class CallContext
{
    /// <summary>
    /// Get the AccountGrain of the caller.
    /// Returns null if caller is anonymous (Open mode) or internal grain call.
    /// </summary>
    public static IAccountGrain? GetCallingAccount()
        => RequestContext.Get("Scynapse.CallingAccount") as IAccountGrain;
    
    /// <summary>
    /// Get the SessionGrain of the caller.
    /// Returns null if caller has no session.
    /// </summary>
    public static ISessionGrain? GetCallingSession()
        => RequestContext.Get("Scynapse.CallingSession") as ISessionGrain;
    
    /// <summary>
    /// Get the ConnectionGrain of the caller.
    /// Returns null if caller is not an external client.
    /// </summary>
    public static IConnectionGrain? GetCallingConnection()
        => RequestContext.Get("Scynapse.CallingConnection") as IConnectionGrain;
    
    /// <summary>
    /// Check if the current call is from an authenticated principal.
    /// </summary>
    public static async Task<bool> IsAuthenticatedAsync()
    {
        var account = GetCallingAccount();
        return account != null && await account.IsAuthenticatedAsync();
    }
    
    /// <summary>
    /// Require authentication, throwing if not authenticated.
    /// </summary>
    public static async Task<IAccountGrain> RequireAuthenticationAsync()
    {
        var account = GetCallingAccount();
        if (account == null || !await account.IsAuthenticatedAsync())
            throw new AuthenticationException("Authentication required");
        return account;
    }
    
    /// <summary>
    /// Require a capability, throwing if not authorized.
    /// </summary>
    public static async Task RequireCapabilityAsync(string capability)
    {
        var account = await RequireAuthenticationAsync();
        if (!await account.HasCapabilityAsync(capability))
            throw new AuthorizationException($"Capability '{capability}' required");
    }
}
```

---

*End of Document*
