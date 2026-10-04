// STUB (compile-check only): Unity Gaming Services (Core / Authentication / Multiplayer Services). Not functional.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.Core
{
    public enum ServicesInitializationState { Uninitialized, Initializing, Initialized }
    public class InitializationOptions { public InitializationOptions SetProfile(string profile) => this; public InitializationOptions SetEnvironmentName(string env) => this; }
    public static class UnityServices
    {
        public static ServicesInitializationState State { get; }
        public static event Action Initialized;
        public static Task InitializeAsync() => Task.CompletedTask;
        public static Task InitializeAsync(InitializationOptions options) => Task.CompletedTask;
    }
    public class ServicesInitializationException : Exception { }
    public class RequestFailedException : Exception { public int ErrorCode { get; } public RequestFailedException(int errorCode, string message) : base(message) { } }
}

namespace Unity.Services.Authentication
{
    public class SignInOptions { public bool CreateAccount; }
    public interface IAuthenticationService
    {
        bool IsSignedIn { get; } bool IsAuthorized { get; } bool IsExpired { get; }
        string PlayerId { get; } string AccessToken { get; } string PlayerName { get; }
        event Action SignedIn; event Action SignedOut; event Action Expired; event Action<Unity.Services.Core.RequestFailedException> SignInFailed;
        Task SignInAnonymouslyAsync(SignInOptions options = null);
        Task<string> GetPlayerNameAsync(bool autoGenerate = true);
        Task<string> UpdatePlayerNameAsync(string playerName);
        void SignOut(bool clearCredentials = false);
    }
    public static class AuthenticationService { public static IAuthenticationService Instance { get; } }
    public class AuthenticationException : Unity.Services.Core.RequestFailedException { public AuthenticationException(int code, string message) : base(code, message) { } }
}

namespace Unity.Services.Multiplayer
{
    public class FilterOption { public FilterOption(FilterField field, string value, FilterOperation op) { } }
    public enum FilterField { Name, MaxPlayers, AvailableSlots, IsLocked, HasPassword, Created, LastUpdated, Strings1, Strings2, Strings3, Strings4, Strings5, Numbers1, Numbers2, Numbers3, Numbers4, Numbers5 }
    public enum FilterOperation { Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual, Contains }
    public class QuickJoinOptions { public TimeSpan Timeout { get; set; } public bool CreateSession { get; set; } public List<FilterOption> Filters { get; set; } }
    public class JoinSessionOptions { public string Password { get; set; } public Dictionary<string, PlayerProperty> PlayerProperties { get; set; } }
    public class SessionOptions
    {
        public int MaxPlayers { get; set; } public bool IsPrivate { get; set; } public bool IsLocked { get; set; } public string Name { get; set; } public string Password { get; set; }
        public Dictionary<string, SessionProperty> SessionProperties { get; set; } public Dictionary<string, PlayerProperty> PlayerProperties { get; set; }
    }
    public class RelayNetworkOptions { public string Region; }
    public static class SessionOptionsExtensions
    {
        public static SessionOptions WithRelayNetwork(this SessionOptions options, string region = null) => options;
        public static SessionOptions WithRelayNetwork(this SessionOptions options, RelayNetworkOptions relayOptions) => options;
    }
    public enum VisibilityPropertyOptions { Public, Member, Private, Index }
    public class SessionProperty { public SessionProperty(string value, VisibilityPropertyOptions visibility = VisibilityPropertyOptions.Public) { } public string Value { get; } }
    public class PlayerProperty { public PlayerProperty(string value, VisibilityPropertyOptions visibility = VisibilityPropertyOptions.Member) { } public string Value { get; } }
    public interface IReadOnlyPlayer { string Id { get; } IReadOnlyDictionary<string, PlayerProperty> Properties { get; } }
    public interface ISession
    {
        string Id { get; } string Name { get; } string Code { get; } string Host { get; }
        int MaxPlayers { get; } int PlayerCount { get; } int AvailablePlayerSlots { get; }
        bool IsHost { get; } bool IsPrivate { get; } bool IsLocked { get; }
        IReadOnlyList<IReadOnlyPlayer> Players { get; }
        IReadOnlyDictionary<string, SessionProperty> Properties { get; }
        event Action Changed; event Action<string> PlayerJoined; event Action<string> PlayerLeaving; event Action<string> PlayerHasLeft; event Action Deleted;
        Task LeaveAsync();
        IHostSession AsHost();
    }
    public interface IHostSession : ISession
    {
        Task DeleteAsync(); Task SavePropertiesAsync();
        Task RemovePlayerAsync(string playerId);
        IHostSession SetLocked(bool locked); IHostSession SetPrivate(bool isPrivate); IHostSession SetName(string name); IHostSession SetMaxPlayers(int maxPlayers);
    }
    public interface IMultiplayerService
    {
        Task<ISession> CreateSessionAsync(SessionOptions sessionOptions);
        Task<ISession> JoinSessionByCodeAsync(string sessionCode, JoinSessionOptions options = null);
        Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions options = null);
        Task<ISession> MatchmakeSessionAsync(QuickJoinOptions quickJoinOptions, SessionOptions sessionOptions, CancellationToken cancellationToken = default);
        Task<ISession> CreateOrJoinSessionAsync(string sessionId, SessionOptions sessionOptions);
        IReadOnlyDictionary<string, ISession> Sessions { get; }
    }
    public static class MultiplayerService { public static IMultiplayerService Instance { get; } }
    public class SessionException : Exception { public SessionError Error { get; } }
    public enum SessionError { None, Unknown, SessionNotFound, SessionFull, InvalidPassword, MatchmakerTimeout }
}
