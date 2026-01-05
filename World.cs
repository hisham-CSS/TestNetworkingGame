using System;
using System.Collections.Generic;
namespace Bomberman
{
    public class World
    {
        private List<Entity> _entities = new();
        private Dictionary<Entity, TransformComponent> _transforms = new();
        private Dictionary<Entity, PlayerComponent> _players = new();
        private Dictionary<Entity, BombComponent> _bombs = new();
        private Dictionary<Entity, ExplosionComponent> _explosions = new();
        private uint _nextId = 1;
        
        public Entity CreateEntity()
        {
            var entity = new Entity(_nextId++, 0);
            _entities.Add(entity);
            return entity;
        }
        
        public void AddTransform(Entity entity, TransformComponent transform)
        {
            _transforms[entity] = transform;
        }
        public void AddPlayer(Entity entity, PlayerComponent player)
        {
            _players[entity] = player;
        }
        public void AddBomb(Entity entity, BombComponent bomb)
        {
            _bombs[entity] = bomb;
        }
        public void AddExplosion(Entity entity, ExplosionComponent explosion)
        {
            _explosions[entity] = explosion;
        }                
        public TransformComponent? GetTransform(Entity entity)
        {
            return _transforms.TryGetValue(entity, out var t) ? t : null;
        }
        public PlayerComponent? GetPlayer(Entity entity)
        {
            return _players.TryGetValue(entity, out var p) ? p : null;
        }
        public BombComponent? GetBomb(Entity entity)
        {
            return _bombs.TryGetValue(entity, out var b) ? b : null;
        }
        public ExplosionComponent? GetExplosion(Entity entity)
        {
            return _explosions.TryGetValue(entity, out var e) ? e : null;
        }
        
        public void RemoveExplosion(Entity entity)
        {
            _explosions.Remove(entity);
        }   
        public void RemoveBomb(Entity entity)
        {
            _bombs.Remove(entity);
        }        
        public List<Entity> GetEntities() => _entities;
    }
}