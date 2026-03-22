using System.Text.Json;
using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests.Models;

public class CombatStateSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [Fact]
    public void Serialize_DefaultCombatState_HasCorrectDefaults()
    {
        var combat = new CombatState();

        var json = JsonSerializer.Serialize(combat, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("turn").GetInt32());
        Assert.NotNull(root.GetProperty("player"));
        Assert.Equal(0, root.GetProperty("monsters").GetArrayLength());
        Assert.Equal(0, root.GetProperty("hand").GetArrayLength());
        Assert.Equal(0, root.GetProperty("drawPile").GetArrayLength());
        Assert.Equal(0, root.GetProperty("discardPile").GetArrayLength());
        Assert.Equal(0, root.GetProperty("exhaustPile").GetArrayLength());
    }

    [Fact]
    public void Serialize_NestedPlayerState_SerializesCorrectly()
    {
        var combat = new CombatState
        {
            Turn = 5,
            Player = new PlayerState
            {
                Hp = 45,
                MaxHp = 72,
                Block = 15,
                Energy = 1,
                MaxEnergy = 3,
                Gold = 200,
                Powers = new()
                {
                    new PowerInfo { Id = "strength", Name = "Strength", Amount = 3 },
                    new PowerInfo { Id = "dexterity", Name = "Dexterity", Amount = -1 },
                },
                Potions = new()
                {
                    new PotionInfo { Id = "fire_potion", Name = "Fire Potion", Description = "Deal 20 damage.", CanUse = true },
                },
            },
        };

        var json = JsonSerializer.Serialize(combat, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var player = doc.RootElement.GetProperty("player");

        Assert.Equal(45, player.GetProperty("hp").GetInt32());
        Assert.Equal(72, player.GetProperty("maxHp").GetInt32());
        Assert.Equal(15, player.GetProperty("block").GetInt32());
        Assert.Equal(1, player.GetProperty("energy").GetInt32());
        Assert.Equal(3, player.GetProperty("maxEnergy").GetInt32());
        Assert.Equal(200, player.GetProperty("gold").GetInt32());
        Assert.Equal(2, player.GetProperty("powers").GetArrayLength());
        Assert.Equal("strength", player.GetProperty("powers")[0].GetProperty("id").GetString());
        Assert.Equal(3, player.GetProperty("powers")[0].GetProperty("amount").GetInt32());
        Assert.Single(JsonSerializer.Deserialize<List<PotionInfo>>(player.GetProperty("potions").GetRawText(), JsonOptions)!);
    }

    [Fact]
    public void Serialize_NestedMonsterInfo_SerializesCorrectly()
    {
        var combat = new CombatState
        {
            Monsters = new()
            {
                new MonsterInfo
                {
                    Id = "cultist",
                    Name = "Cultist",
                    Hp = 48,
                    MaxHp = 56,
                    Block = 0,
                    Intent = "attack",
                    IntentDamage = 6,
                    Powers = new()
                    {
                        new PowerInfo { Id = "ritual", Name = "Ritual", Amount = 3 },
                    },
                },
                new MonsterInfo
                {
                    Id = "jaw_worm",
                    Name = "Jaw Worm",
                    Hp = 20,
                    MaxHp = 44,
                    Block = 5,
                    Intent = "defend",
                    IntentDamage = 0,
                },
            },
        };

        var json = JsonSerializer.Serialize(combat, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var monsters = doc.RootElement.GetProperty("monsters");

        Assert.Equal(2, monsters.GetArrayLength());

        var cultist = monsters[0];
        Assert.Equal("cultist", cultist.GetProperty("id").GetString());
        Assert.Equal("Cultist", cultist.GetProperty("name").GetString());
        Assert.Equal(48, cultist.GetProperty("hp").GetInt32());
        Assert.Equal(56, cultist.GetProperty("maxHp").GetInt32());
        Assert.Equal("attack", cultist.GetProperty("intent").GetString());
        Assert.Equal(6, cultist.GetProperty("intentDamage").GetInt32());
        Assert.Equal(1, cultist.GetProperty("powers").GetArrayLength());

        var jawWorm = monsters[1];
        Assert.Equal("jaw_worm", jawWorm.GetProperty("id").GetString());
        Assert.Equal(5, jawWorm.GetProperty("block").GetInt32());
        Assert.Equal("defend", jawWorm.GetProperty("intent").GetString());
        Assert.Equal(0, jawWorm.GetProperty("powers").GetArrayLength());
    }

    [Fact]
    public void Serialize_EmptyLists_SerializeAsEmptyArrays()
    {
        var combat = new CombatState
        {
            Hand = new(),
            DrawPile = new(),
            DiscardPile = new(),
            ExhaustPile = new(),
            Monsters = new(),
        };

        var json = JsonSerializer.Serialize(combat, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("hand").GetArrayLength());
        Assert.Equal(0, root.GetProperty("drawPile").GetArrayLength());
        Assert.Equal(0, root.GetProperty("discardPile").GetArrayLength());
        Assert.Equal(0, root.GetProperty("exhaustPile").GetArrayLength());
        Assert.Equal(0, root.GetProperty("monsters").GetArrayLength());
    }

    [Fact]
    public void Roundtrip_FullCombatState_PreservesAllData()
    {
        var combat = new CombatState
        {
            Turn = 7,
            Player = new PlayerState
            {
                Hp = 30,
                MaxHp = 80,
                Block = 20,
                Energy = 0,
                MaxEnergy = 4,
                Gold = 350,
                Powers = new() { new PowerInfo { Id = "vigor", Name = "Vigor", Amount = 5 } },
                Potions = new() { new PotionInfo { Id = "block_potion", Name = "Block Potion", Description = "Gain 12 Block.", CanUse = true } },
            },
            Monsters = new()
            {
                new MonsterInfo { Id = "gremlin_nob", Name = "Gremlin Nob", Hp = 82, MaxHp = 110, Intent = "attack", IntentDamage = 16 },
            },
            Hand = new()
            {
                new CardInfo { Id = "strike", Name = "Strike", Cost = 1 },
                new CardInfo { Id = "defend", Name = "Defend", Cost = 1 },
            },
            DrawPile = new()
            {
                new CardInfo { Id = "bash", Name = "Bash", Cost = 2 },
            },
            DiscardPile = new()
            {
                new CardInfo { Id = "anger", Name = "Anger", Cost = 0 },
            },
            ExhaustPile = new()
            {
                new CardInfo { Id = "true_grit", Name = "True Grit", Cost = 1 },
            },
        };

        var json = JsonSerializer.Serialize(combat, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CombatState>(json, JsonOptions)!;

        Assert.Equal(7, deserialized.Turn);
        Assert.Equal(30, deserialized.Player.Hp);
        Assert.Equal(80, deserialized.Player.MaxHp);
        Assert.Equal(20, deserialized.Player.Block);
        Assert.Equal(0, deserialized.Player.Energy);
        Assert.Equal(4, deserialized.Player.MaxEnergy);
        Assert.Equal(350, deserialized.Player.Gold);
        Assert.Single(deserialized.Player.Powers);
        Assert.Single(deserialized.Player.Potions);
        Assert.Single(deserialized.Monsters);
        Assert.Equal(2, deserialized.Hand.Count);
        Assert.Single(deserialized.DrawPile);
        Assert.Single(deserialized.DiscardPile);
        Assert.Single(deserialized.ExhaustPile);
    }

    [Fact]
    public void Serialize_PlayerState_DefaultMaxEnergy_IsThree()
    {
        var player = new PlayerState();

        Assert.Equal(3, player.MaxEnergy);

        var json = JsonSerializer.Serialize(player, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(3, doc.RootElement.GetProperty("maxEnergy").GetInt32());
    }

    [Fact]
    public void Serialize_MonsterInfo_DefaultIntent_IsUnknown()
    {
        var monster = new MonsterInfo();

        Assert.Equal("unknown", monster.Intent);

        var json = JsonSerializer.Serialize(monster, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("unknown", doc.RootElement.GetProperty("intent").GetString());
    }
}
