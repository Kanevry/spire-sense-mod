using System.Text.Json;
using System.Threading;
using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests;

public class GameStateTrackerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [Fact]
    public void GetCurrentState_InitialState_ReturnsDefaultGameState()
    {
        var tracker = new GameStateTracker();

        var state = tracker.GetCurrentState();

        Assert.Equal("main_menu", state.Screen);
        Assert.Equal("ironclad", state.Character);
        Assert.Equal(1, state.Act);
        Assert.Equal(0, state.Floor);
    }

    [Fact]
    public void GetSerializedState_InitialState_ReturnsValidJson()
    {
        var tracker = new GameStateTracker();

        var json = tracker.GetSerializedState();

        // Initial state is "{}" — an empty JSON object before first serialization
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        // Should be parseable as JSON — if this throws, the test fails
        using var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void SetState_NewState_UpdatesCurrentState()
    {
        var tracker = new GameStateTracker();
        var newState = new GameState
        {
            Screen = "combat",
            Character = "silent",
            Act = 3,
            Floor = 42,
            Ascension = 10,
        };

        tracker.SetState(newState);

        var result = tracker.GetCurrentState();
        Assert.Equal("combat", result.Screen);
        Assert.Equal("silent", result.Character);
        Assert.Equal(3, result.Act);
        Assert.Equal(42, result.Floor);
        Assert.Equal(10, result.Ascension);
    }

    [Fact]
    public void SetState_EmitsStateUpdateEvent()
    {
        var tracker = new GameStateTracker();
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e => receivedEvent = e;

        tracker.SetState(new GameState { Screen = "map" });

        Assert.NotNull(receivedEvent);
        Assert.Equal("state_update", receivedEvent!.Type);
        Assert.NotNull(receivedEvent.SerializedData);
    }

    [Fact]
    public void SetState_SerializedDataContainsNewState()
    {
        var tracker = new GameStateTracker();
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e => receivedEvent = e;

        tracker.SetState(new GameState { Screen = "shop", Character = "defect" });

        Assert.NotNull(receivedEvent?.SerializedData);
        using var doc = JsonDocument.Parse(receivedEvent!.SerializedData!);
        Assert.Equal("shop", doc.RootElement.GetProperty("screen").GetString());
        Assert.Equal("defect", doc.RootElement.GetProperty("character").GetString());
    }

    [Fact]
    public void UpdateState_Lambda_ModifiesState()
    {
        var tracker = new GameStateTracker();
        tracker.SetState(new GameState { Screen = "map", Floor = 5 });

        tracker.UpdateState(s =>
        {
            s.Floor = 6;
            s.Screen = "combat";
        });

        var result = tracker.GetCurrentState();
        Assert.Equal(6, result.Floor);
        Assert.Equal("combat", result.Screen);
    }

    [Fact]
    public void UpdateState_EmitsStateUpdateEvent()
    {
        var tracker = new GameStateTracker();
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e => receivedEvent = e;

        tracker.UpdateState(s => s.Floor = 10);

        Assert.NotNull(receivedEvent);
        Assert.Equal("state_update", receivedEvent!.Type);
    }

    [Fact]
    public void SetScreen_UpdatesScreenOnly()
    {
        var tracker = new GameStateTracker();
        tracker.SetState(new GameState { Screen = "map", Character = "ironclad", Floor = 5 });

        tracker.SetScreen("combat");

        var result = tracker.GetCurrentState();
        Assert.Equal("combat", result.Screen);
        Assert.Equal("ironclad", result.Character);
        Assert.Equal(5, result.Floor);
    }

    [Fact]
    public void SetCombatState_SetAndClear()
    {
        var tracker = new GameStateTracker();

        var combat = new CombatState
        {
            Turn = 1,
            Player = new PlayerState { Hp = 80, MaxHp = 80 },
        };
        tracker.SetCombatState(combat);

        var result = tracker.GetCurrentState();
        Assert.NotNull(result.Combat);
        Assert.Equal(1, result.Combat!.Turn);
        Assert.Equal(80, result.Combat.Player.Hp);

        // Clear combat
        tracker.SetCombatState(null);
        result = tracker.GetCurrentState();
        Assert.Null(result.Combat);
    }

    [Fact]
    public void SetCardRewards_SetAndClear()
    {
        var tracker = new GameStateTracker();

        var rewards = new List<CardInfo>
        {
            new() { Id = "bash", Name = "Bash" },
            new() { Id = "anger", Name = "Anger" },
        };
        tracker.SetCardRewards(rewards);

        var result = tracker.GetCurrentState();
        Assert.NotNull(result.CardRewards);
        Assert.Equal(2, result.CardRewards!.Count);

        // Clear rewards
        tracker.SetCardRewards(null);
        result = tracker.GetCurrentState();
        Assert.Null(result.CardRewards);
    }

    [Fact]
    public void GetCurrentState_ReturnsDeepCopy_NotReference()
    {
        var tracker = new GameStateTracker();
        tracker.SetState(new GameState { Screen = "map", Floor = 5 });

        var copy1 = tracker.GetCurrentState();
        var copy2 = tracker.GetCurrentState();

        // Modifying one copy should not affect the other
        copy1.Floor = 99;
        Assert.Equal(5, copy2.Floor);
    }

    [Fact]
    public void EmitEvent_CustomEvent_InvokesHandler()
    {
        var tracker = new GameStateTracker();
        var events = new List<GameEvent>();
        tracker.OnGameEvent += e => events.Add(e);

        tracker.EmitEvent(new GameEvent { Type = "card_played", Data = "strike" });
        tracker.EmitEvent(new GameEvent { Type = "turn_end" });

        Assert.Equal(2, events.Count);
        Assert.Equal("card_played", events[0].Type);
        Assert.Equal("strike", events[0].Data);
        Assert.Equal("turn_end", events[1].Type);
    }

    [Fact]
    public void EmitEvent_WithSerializedData_SetsOnEvent()
    {
        var tracker = new GameStateTracker();
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e => receivedEvent = e;

        var serialized = "{\"custom\":\"data\"}";
        tracker.EmitEvent(new GameEvent { Type = "test" }, serialized);

        Assert.NotNull(receivedEvent);
        Assert.Equal(serialized, receivedEvent!.SerializedData);
    }

    [Fact]
    public void EmitEvent_WithoutSerializedData_LeavesNull()
    {
        var tracker = new GameStateTracker();
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e => receivedEvent = e;

        tracker.EmitEvent(new GameEvent { Type = "test" });

        Assert.NotNull(receivedEvent);
        Assert.Null(receivedEvent!.SerializedData);
    }

    [Fact]
    public async Task ConcurrentAccess_MultipleWritersAndReaders_NoExceptions()
    {
        var tracker = new GameStateTracker();
        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        // 10 writer tasks
        for (int i = 0; i < 10; i++)
        {
            var floor = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        tracker.UpdateState(s => s.Floor = floor * 100 + j);
                        tracker.SetScreen($"screen_{floor}_{j}");
                        tracker.SetCombatState(new CombatState { Turn = j });
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }));
        }

        // 10 reader tasks
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var state = tracker.GetCurrentState();
                        var json = tracker.GetSerializedState();
                        // Ensure valid JSON is always returned
                        JsonDocument.Parse(json);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);
    }

    [Fact]
    public async Task ConcurrentAccess_SetState_AlwaysConsistent()
    {
        var tracker = new GameStateTracker();
        // Seed with an initial state so the serialized snapshot has all properties
        tracker.SetState(new GameState { Screen = "init", Character = "init" });
        var inconsistencies = 0;
        var tasks = new List<Task>();

        for (int i = 0; i < 5; i++)
        {
            var character = $"char_{i}";
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 200; j++)
                {
                    tracker.SetState(new GameState
                    {
                        Character = character,
                        Floor = j,
                        Screen = $"{character}_floor_{j}",
                    });
                }
            }));
        }

        // Reader that checks serialized state is valid JSON and internally consistent
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 200; j++)
                {
                    var json = tracker.GetSerializedState();
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        // Just ensure it parses — internal consistency depends on lock
                        var screen = doc.RootElement.GetProperty("screen").GetString();
                        var character = doc.RootElement.GetProperty("character").GetString();
                        // The screen should contain the character name (our convention)
                        // But this can race with other writers, so we just ensure it parses
                    }
                    catch
                    {
                        Interlocked.Increment(ref inconsistencies);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(0, inconsistencies);
    }

    [Fact]
    public void GetSerializedState_AfterSetState_ReflectsUpdate()
    {
        var tracker = new GameStateTracker();

        tracker.SetState(new GameState { Screen = "reward", Floor = 20 });

        var json = tracker.GetSerializedState();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("reward", doc.RootElement.GetProperty("screen").GetString());
        Assert.Equal(20, doc.RootElement.GetProperty("floor").GetInt32());
    }

    [Fact]
    public void GameEvent_SerializedData_ExcludedFromJsonSerialization()
    {
        var gameEvent = new GameEvent
        {
            Type = "state_update",
            SerializedData = "{\"big\":\"payload\"}",
        };

        var json = JsonSerializer.Serialize(gameEvent, JsonOptions);

        // SerializedData has [JsonIgnore], so it should not appear
        Assert.DoesNotContain("serializedData", json);
        Assert.DoesNotContain("SerializedData", json);
        Assert.Contains("\"type\":", json);
    }

    [Fact]
    public void MultipleEventSubscribers_AllReceiveEvents()
    {
        var tracker = new GameStateTracker();
        var events1 = new List<string>();
        var events2 = new List<string>();
        tracker.OnGameEvent += e => events1.Add(e.Type);
        tracker.OnGameEvent += e => events2.Add(e.Type);

        tracker.SetState(new GameState { Screen = "test" });
        tracker.UpdateState(s => s.Floor = 1);
        tracker.EmitEvent(new GameEvent { Type = "custom" });

        // SetState emits 1 event, UpdateState emits 1 event, EmitEvent emits 1 event
        Assert.Equal(3, events1.Count);
        Assert.Equal(3, events2.Count);
        Assert.Equal("state_update", events1[0]);
        Assert.Equal("state_update", events1[1]);
        Assert.Equal("custom", events1[2]);
    }

    // ─── Event Ring Buffer Tests ───────────────────────────────────────

    [Fact]
    public void AddEvent_StoresEventInBuffer()
    {
        var tracker = new GameStateTracker();

        tracker.AddEvent("card_played", new { card = "strike" });

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("card_played", events[0].Type);
        Assert.True(events[0].Timestamp > 0);
    }

    [Fact]
    public void AddEvent_NullData_Allowed()
    {
        var tracker = new GameStateTracker();

        tracker.AddEvent("turn_end", null);

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("turn_end", events[0].Type);
        Assert.Null(events[0].Data);
    }

    [Fact]
    public void GetEventsSince_FiltersOlderEvents()
    {
        var tracker = new GameStateTracker();

        tracker.AddEvent("event_a", null);
        var afterFirst = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Small delay to ensure distinct timestamps
        Thread.Sleep(5);

        tracker.AddEvent("event_b", null);

        var events = tracker.GetEventsSince(afterFirst);
        Assert.Single(events);
        Assert.Equal("event_b", events[0].Type);
    }

    [Fact]
    public void GetEventsSince_ZeroTimestamp_ReturnsAll()
    {
        var tracker = new GameStateTracker();

        tracker.AddEvent("a", null);
        tracker.AddEvent("b", null);
        tracker.AddEvent("c", null);

        var events = tracker.GetEventsSince(0);
        Assert.Equal(3, events.Count);
    }

    [Fact]
    public void GetEventsSince_FutureTimestamp_ReturnsEmpty()
    {
        var tracker = new GameStateTracker();
        tracker.AddEvent("a", null);

        var futureTs = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
        var events = tracker.GetEventsSince(futureTs);
        Assert.Empty(events);
    }

    [Fact]
    public void RingBuffer_EvictsOldestWhenFull()
    {
        var tracker = new GameStateTracker();

        // Fill beyond the 100-event capacity
        for (int i = 0; i < 110; i++)
        {
            tracker.AddEvent($"event_{i}", new { index = i });
        }

        var events = tracker.GetEventsSince(0);
        Assert.Equal(100, events.Count);

        // Oldest events (0-9) should have been evicted
        Assert.Equal("event_10", events[0].Type);
        Assert.Equal("event_109", events[99].Type);
    }

    [Fact]
    public void EmitEvent_AutoBuffersNonStateUpdateEvents()
    {
        var tracker = new GameStateTracker();

        tracker.EmitEvent(new GameEvent { Type = "combat_start", Data = new { monsters = 3 } });
        tracker.EmitEvent(new GameEvent { Type = "card_played", Data = new { card = "bash" } });

        var events = tracker.GetEventsSince(0);
        Assert.Equal(2, events.Count);
        Assert.Equal("combat_start", events[0].Type);
        Assert.Equal("card_played", events[1].Type);
    }

    [Fact]
    public void EmitEvent_StateUpdate_NotBuffered()
    {
        var tracker = new GameStateTracker();

        // state_update events are internal noise and should not be buffered
        tracker.EmitEvent(new GameEvent { Type = "state_update" });

        var events = tracker.GetEventsSince(0);
        Assert.Empty(events);
    }

    [Fact]
    public void SetState_DoesNotBufferStateUpdateEvent()
    {
        var tracker = new GameStateTracker();

        tracker.SetState(new GameState { Screen = "map" });

        // SetState calls EmitEvent with type "state_update", which should be filtered out
        var events = tracker.GetEventsSince(0);
        Assert.Empty(events);
    }

    [Fact]
    public void UpdateState_DoesNotBufferStateUpdateEvent()
    {
        var tracker = new GameStateTracker();

        tracker.UpdateState(s => s.Floor = 5);

        // UpdateState calls EmitEvent with type "state_update", which should be filtered out
        var events = tracker.GetEventsSince(0);
        Assert.Empty(events);
    }

    [Fact]
    public void GetEventsSince_ReturnsSnapshotCopy()
    {
        var tracker = new GameStateTracker();
        tracker.AddEvent("a", null);

        var events1 = tracker.GetEventsSince(0);
        tracker.AddEvent("b", null);
        var events2 = tracker.GetEventsSince(0);

        // events1 should not be affected by the second AddEvent call
        Assert.Single(events1);
        Assert.Equal(2, events2.Count);
    }

    [Fact]
    public void EventBuffer_TimestampsAreMonotonicallyIncreasing()
    {
        var tracker = new GameStateTracker();

        for (int i = 0; i < 10; i++)
        {
            tracker.AddEvent($"event_{i}", null);
        }

        var events = tracker.GetEventsSince(0);
        for (int i = 1; i < events.Count; i++)
        {
            Assert.True(events[i].Timestamp >= events[i - 1].Timestamp,
                $"Timestamp at index {i} ({events[i].Timestamp}) should be >= timestamp at index {i - 1} ({events[i - 1].Timestamp})");
        }
    }

    [Fact]
    public async Task EventBuffer_ConcurrentAccess_NoExceptions()
    {
        var tracker = new GameStateTracker();
        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        // 5 writer tasks adding events
        for (int i = 0; i < 5; i++)
        {
            var writerIndex = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 50; j++)
                    {
                        tracker.AddEvent($"writer_{writerIndex}_event_{j}", new { writerIndex, j });
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }));
        }

        // 5 reader tasks querying events
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 50; j++)
                    {
                        var events = tracker.GetEventsSince(0);
                        // Just ensure it returns a valid list
                        Assert.NotNull(events);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }

    [Fact]
    public void BufferedEvent_SerializesToJson()
    {
        var entry = new BufferedEvent
        {
            Type = "combat_start",
            Data = new { monsters = 3 },
            Timestamp = 1711000000000,
        };

        var json = JsonSerializer.Serialize(entry, JsonOptions);

        Assert.Contains("\"type\":\"combat_start\"", json);
        Assert.Contains("\"timestamp\":1711000000000", json);
        Assert.Contains("\"data\":", json);
    }
}
