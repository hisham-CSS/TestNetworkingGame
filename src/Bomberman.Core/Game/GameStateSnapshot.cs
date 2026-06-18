using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;

namespace Bomberman.Core
{
    /// <summary>
    /// A complete, restorable copy of the World at one frame: every component pool's parallel
    /// (entity, data) lists, the next-entity id, the frame number, and the state hash at capture time.
    ///
    /// Three jobs this week:
    ///   1. Desync detection - the stored Hash is the per-frame fingerprint compared across the net.
    ///   2. Resync - serialize to bytes, ship via StateSync, deserialize and Restore on the other peer.
    ///   3. Foundation for Week 5 rollback - Restore() rewinds the world to a past frame.
    ///
    /// Serialization is BINARY (BinaryWriter): compact and with a fixed, deterministic layout - exactly
    /// what you want on the wire. (The production Chronos used JSON; binary is smaller and faster.)
    /// </summary>
    public sealed class GameStateSnapshot
    {
        public int Frame { get; private set; }
        public uint NextEntityId { get; private set; }
        public int Hash { get; private set; }

        // One captured pool = parallel entity/component lists (copies, never aliases of the live world).
        private (List<Entity> e, List<TransformComponent> c) _transforms;
        private (List<Entity> e, List<PlayerComponent> c) _players;
        private (List<Entity> e, List<BombComponent> c) _bombs;
        private (List<Entity> e, List<ExplosionComponent> c) _explosions;
        private (List<Entity> e, List<TileComponent> c) _tiles;
        private (List<Entity> e, List<PowerupComponent> c) _powerups;

        private GameStateSnapshot() { }

        /// <summary>Capture the world as it stands at <paramref name="frame"/>.</summary>
        public static GameStateSnapshot Capture(World world, int frame)
        {
            var s = new GameStateSnapshot
            {
                Frame = frame,
                NextEntityId = world.NextEntityId,
                _transforms = world.Transforms.CaptureState(),
                _players = world.Players.CaptureState(),
                _bombs = world.Bombs.CaptureState(),
                _explosions = world.Explosions.CaptureState(),
                _tiles = world.Tiles.CaptureState(),
                _powerups = world.Powerups.CaptureState(),
            };
            s.Hash = StateHasher.Hash(world); // fingerprint of THIS state
            return s;
        }

        /// <summary>Overwrite <paramref name="world"/> with this snapshot's contents.</summary>
        public void Restore(World world)
        {
            world.Clear();
            world.NextEntityId = NextEntityId;
            world.Transforms.RestoreState(_transforms.e, _transforms.c);
            world.Players.RestoreState(_players.e, _players.c);
            world.Bombs.RestoreState(_bombs.e, _bombs.c);
            world.Explosions.RestoreState(_explosions.e, _explosions.c);
            world.Tiles.RestoreState(_tiles.e, _tiles.c);
            world.Powerups.RestoreState(_powerups.e, _powerups.c);
        }

        /// <summary>Recompute the hash from the captured data by restoring into a scratch world. For a
        /// snapshot rebuilt from bytes this MUST equal the original Hash, which both verifies the
        /// round-trip and lets a resynced peer trust the state it just received.</summary>
        public int ComputeHash()
        {
            var w = new World();
            Restore(w);
            return StateHasher.Hash(w);
        }

        // ---------------- binary serialization ----------------

        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(Frame);
            w.Write(NextEntityId);
            w.Write(Hash);
            WriteTransforms(w, _transforms);
            WritePlayers(w, _players);
            WriteBombs(w, _bombs);
            WriteExplosions(w, _explosions);
            WriteTiles(w, _tiles);
            WritePowerups(w, _powerups);
            return ms.ToArray();
        }

        public static GameStateSnapshot Deserialize(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);
            var s = new GameStateSnapshot
            {
                Frame = r.ReadInt32(),
                NextEntityId = r.ReadUInt32(),
                Hash = r.ReadInt32(),
            };
            s._transforms = ReadTransforms(r);
            s._players = ReadPlayers(r);
            s._bombs = ReadBombs(r);
            s._explosions = ReadExplosions(r);
            s._tiles = ReadTiles(r);
            s._powerups = ReadPowerups(r);
            return s;
        }

        // per-pool writers/readers. Entities are a single uint; structs are written field by field.
        private static void WriteHeader(BinaryWriter w, List<Entity> e) => w.Write(e.Count);

        private static void WriteTransforms(BinaryWriter w, (List<Entity> e, List<TransformComponent> c) p)
        {
            WriteHeader(w, p.e);
            for (int i = 0; i < p.e.Count; i++)
            {
                w.Write(p.e[i].Index);
                w.Write(p.c[i].Position.X); w.Write(p.c[i].Position.Y);
                w.Write(p.c[i].Size.X); w.Write(p.c[i].Size.Y);
            }
        }
        private static (List<Entity>, List<TransformComponent>) ReadTransforms(BinaryReader r)
        {
            int n = r.ReadInt32(); var e = new List<Entity>(n); var c = new List<TransformComponent>(n);
            for (int i = 0; i < n; i++)
            {
                e.Add(new Entity(r.ReadUInt32()));
                c.Add(new TransformComponent { Position = new Vector2(r.ReadSingle(), r.ReadSingle()), Size = new Vector2(r.ReadSingle(), r.ReadSingle()) });
            }
            return (e, c);
        }

        private static void WritePlayers(BinaryWriter w, (List<Entity> e, List<PlayerComponent> c) p)
        {
            WriteHeader(w, p.e);
            for (int i = 0; i < p.e.Count; i++)
            {
                w.Write(p.e[i].Index);
                w.Write(p.c[i].PlayerId); w.Write(p.c[i].Alive);
                w.Write(p.c[i].BombRange); w.Write(p.c[i].BombCapacity);
            }
        }
        private static (List<Entity>, List<PlayerComponent>) ReadPlayers(BinaryReader r)
        {
            int n = r.ReadInt32(); var e = new List<Entity>(n); var c = new List<PlayerComponent>(n);
            for (int i = 0; i < n; i++)
            {
                e.Add(new Entity(r.ReadUInt32()));
                c.Add(new PlayerComponent { PlayerId = r.ReadUInt32(), Alive = r.ReadBoolean(), BombRange = r.ReadInt32(), BombCapacity = r.ReadInt32() });
            }
            return (e, c);
        }

        private static void WriteBombs(BinaryWriter w, (List<Entity> e, List<BombComponent> c) p)
        {
            WriteHeader(w, p.e);
            for (int i = 0; i < p.e.Count; i++)
            {
                w.Write(p.e[i].Index);
                w.Write(p.c[i].Timer); w.Write(p.c[i].MaxTimer); w.Write(p.c[i].Range); w.Write(p.c[i].OwnerId);
            }
        }
        private static (List<Entity>, List<BombComponent>) ReadBombs(BinaryReader r)
        {
            int n = r.ReadInt32(); var e = new List<Entity>(n); var c = new List<BombComponent>(n);
            for (int i = 0; i < n; i++)
            {
                e.Add(new Entity(r.ReadUInt32()));
                c.Add(new BombComponent { Timer = r.ReadInt32(), MaxTimer = r.ReadInt32(), Range = r.ReadInt32(), OwnerId = r.ReadUInt32() });
            }
            return (e, c);
        }

        private static void WriteExplosions(BinaryWriter w, (List<Entity> e, List<ExplosionComponent> c) p)
        {
            WriteHeader(w, p.e);
            for (int i = 0; i < p.e.Count; i++)
            {
                w.Write(p.e[i].Index);
                w.Write(p.c[i].Timer); w.Write(p.c[i].MaxTimer);
            }
        }
        private static (List<Entity>, List<ExplosionComponent>) ReadExplosions(BinaryReader r)
        {
            int n = r.ReadInt32(); var e = new List<Entity>(n); var c = new List<ExplosionComponent>(n);
            for (int i = 0; i < n; i++)
            {
                e.Add(new Entity(r.ReadUInt32()));
                c.Add(new ExplosionComponent { Timer = r.ReadInt32(), MaxTimer = r.ReadInt32() });
            }
            return (e, c);
        }

        private static void WriteTiles(BinaryWriter w, (List<Entity> e, List<TileComponent> c) p)
        {
            WriteHeader(w, p.e);
            for (int i = 0; i < p.e.Count; i++)
            {
                w.Write(p.e[i].Index);
                w.Write((int)p.c[i].Type); w.Write(p.c[i].Destroyed); w.Write((int)p.c[i].HiddenPowerup);
            }
        }
        private static (List<Entity>, List<TileComponent>) ReadTiles(BinaryReader r)
        {
            int n = r.ReadInt32(); var e = new List<Entity>(n); var c = new List<TileComponent>(n);
            for (int i = 0; i < n; i++)
            {
                e.Add(new Entity(r.ReadUInt32()));
                c.Add(new TileComponent { Type = (TileComponent.TileType)r.ReadInt32(), Destroyed = r.ReadBoolean(), HiddenPowerup = (PowerupComponent.PowerupType)r.ReadInt32() });
            }
            return (e, c);
        }

        private static void WritePowerups(BinaryWriter w, (List<Entity> e, List<PowerupComponent> c) p)
        {
            WriteHeader(w, p.e);
            for (int i = 0; i < p.e.Count; i++)
            {
                w.Write(p.e[i].Index);
                w.Write((int)p.c[i].Type);
            }
        }
        private static (List<Entity>, List<PowerupComponent>) ReadPowerups(BinaryReader r)
        {
            int n = r.ReadInt32(); var e = new List<Entity>(n); var c = new List<PowerupComponent>(n);
            for (int i = 0; i < n; i++)
            {
                e.Add(new Entity(r.ReadUInt32()));
                c.Add(new PowerupComponent { Type = (PowerupComponent.PowerupType)r.ReadInt32() });
            }
            return (e, c);
        }
    }
}
