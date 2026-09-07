using EarthGame.Transport;

namespace EarthGame.Server
{
    /// <summary>One connected player. Created by a valid Hello, destroyed by the connection closing.</summary>
    public sealed class PlayerSession
    {
        public PlayerSession(uint sessionId, string name, IConnection connection, long joinedTick)
        {
            SessionId = sessionId;
            Name = name;
            Connection = connection;
            JoinedTick = joinedTick;
        }

        /// <summary>Unique for the life of the server; never reused, so a stale reference cannot alias a new player.</summary>
        public uint SessionId { get; }
        public string Name { get; }
        public IConnection Connection { get; }
        public long JoinedTick { get; }
    }
}
