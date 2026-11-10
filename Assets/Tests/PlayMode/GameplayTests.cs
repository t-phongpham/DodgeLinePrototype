using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DodgeLine.Tests
{
    public sealed class GameplayTests
    {
        private GameManager game;
        private LevelSettings clonedSettings;
        private float previousTimeScale;
        private readonly List<EnemyData> temporaryEnemyData = new List<EnemyData>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Levels/Level1.unity", LoadSceneMode.Single);
            game = Object.FindAnyObjectByType<GameManager>();
            clonedSettings = Object.Instantiate(game.Settings);
            game.Settings = clonedSettings;
            game.Stage = null; // This suite runs isolated combat fixtures, not progression.
            game.GetComponent<CampaignHUD>().enabled = false;
            clonedSettings.waves = CombatScenarios.WispSpacing(clonedSettings);
            game.Placement.obstacles = new[] { new Rect(-4.6f, 2, 1.2f, 1) };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            if (game != null) game.gameObject.SetActive(false);
            Object.Destroy(clonedSettings);
            foreach (var data in temporaryEnemyData) Object.Destroy(data);
            temporaryEnemyData.Clear();
            yield return null;
        }

        private EnemyData FixtureEnemy(EnemyData original, float radius, float speed)
        {
            var data = Object.Instantiate(original);
            data.hitRadius = radius;
            data.speed = speed;
            temporaryEnemyData.Add(data);
            return data;
        }

        private EnemyController Controlled(EnemyData data, float progress)
        {
            game.Settings.waves = null;
            var enemy = game.Spawner.Spawn(data, progress);
            game.StartWave();
            return enemy;
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator PreparationIncomeEarlyStartAndInvalidPlacement()
        {
            Assert.That(game.Resources.Energy, Is.EqualTo(150));
            yield return Steps(10);
            Assert.That(game.Resources.Energy, Is.EqualTo(150));
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, Vector2.zero), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(0, .7f)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(8.9f, 2)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(-4, 2.5f)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(7.6f, 0)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(0, 1.5f)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(-8, 0)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(7, 0)), Is.False);
            Assert.That(game.Resources.Energy, Is.EqualTo(150));
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 1.5f)), Is.True);
            Assert.That(game.Resources.Energy, Is.EqualTo(100));
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5.1f, 1.5f)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(0, 0)), Is.True);
            Assert.That(game.Resources.Energy, Is.EqualTo(65));
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(0, 0)), Is.False);
            game.Placement.Select(BuildChoice.None);
            Assert.That(game.Resources.Energy, Is.EqualTo(65));
            game.StartWave(); game.StartWave();
            Assert.That(game.Resources.Energy, Is.EqualTo(70));
        }

        [UnityTest]
        public IEnumerator RunnerMovesAndAttacksRepeatedlyAfterWindup()
        {
            var enemy = Controlled(game.Settings.runner, 0);
            yield return Steps(25);
            Assert.That(enemy.Progress, Is.EqualTo(.8f).Within(.04f));
            Assert.That(enemy.Position.y, Is.Zero);
            Time.timeScale = 10;
            float timeout = Time.time + 12;
            while (enemy.State != EnemyState.AttackBase && Time.time < timeout) yield return new WaitForFixedUpdate();
            Assert.That(enemy.State, Is.EqualTo(EnemyState.AttackBase));
            Time.timeScale = 1;
            float arrived = game.SimulationTime;
            float progress = enemy.Progress;
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(20));
            yield return Steps(48);
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(20));
            yield return Steps(3);
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(19));
            yield return Steps(100);
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(18));
            Assert.That(enemy.Alive, Is.True);
            Assert.That(enemy.Progress, Is.EqualTo(progress));
            TestContext.WriteLine($"AttackBase arrived at {arrived:F2}; observed HP 20, 19, 18 at windup/interval checks.");
        }

        [UnityTest]
        public IEnumerator FourPhysicsHitsKillRunnerAndPayBountyOnce()
        {
            var enemy = Controlled(game.Settings.runner, game.Path.Length);
            yield return Steps(1);
            int balance = game.Resources.Energy;
            for (int i = 1; i <= 4; i++)
            {
                Projectile.Launch(game, enemy.Position + Vector2.up, Vector2.down, game.Settings.bow);
                yield return Steps(12);
                if (i < 4) Assert.That(enemy.Health, Is.EqualTo(40 - i * 12));
            }
            Assert.That(game.Kills, Is.EqualTo(1));
            Assert.That(game.Hits, Is.EqualTo(4));
            Assert.That(game.Enemies.Count, Is.Zero);
            Assert.That(game.Resources.Energy, Is.GreaterThanOrEqualTo(balance + 6));
            yield return Steps(100);
            Assert.That(game.Kills, Is.EqualTo(1));
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator ProjectilesKeepHeadingExpireAndStopAtBlocker()
        {
            Controlled(game.Settings.runner, 0);
            var shot = Projectile.Launch(game, new Vector2(0, -2), Vector2.right, game.Settings.bow);
            int id = shot.Id;
            yield return Steps(25);
            Assert.That(shot.Position.x, Is.EqualTo(3).Within(.15f));
            Assert.That(shot.Position.y, Is.EqualTo(-2).Within(.001f));
            yield return Steps(130);
            Assert.That(game.Projectiles.Exists(p => p.Id == id), Is.False);
            var obstacle = new GameObject("Projectile blocker fixture");
            obstacle.transform.SetParent(game.Entities, false); obstacle.transform.position = new Vector2(-4, 2.5f);
            obstacle.AddComponent<ProjectileBlocker>();
            var collider = obstacle.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.size = Vector2.one;
            var blocked = Projectile.Launch(game, new Vector2(-6, 2.5f), Vector2.right, game.Settings.bow);
            int blockerId = blocked.Id;
            yield return Steps(20);
            Assert.That(game.Projectiles.Exists(p => p.Id == blockerId), Is.False);
            Assert.That(game.ExpiredShots, Is.EqualTo(2));
            Assert.That(game.Hits, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TrapUsesNonBlockingPhysicsTriggerAndSlowExpires()
        {
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(0, 0)), Is.True);
            var trap = game.Traps[0];
            Assert.That(trap.GetComponent<Collider2D>().isTrigger, Is.True);
            var enemy = Controlled(game.Settings.runner, 6.9f);
            yield return Steps(10);
            Assert.That(trap.RemainingUses, Is.EqualTo(5));
            Assert.That(enemy.SpeedMultiplier, Is.EqualTo(.5f));
            float progress = enemy.Progress;
            yield return Steps(25);
            Assert.That(enemy.Progress - progress, Is.EqualTo(.4f).Within(.04f));
            yield return Steps(100);
            Assert.That(enemy.SpeedMultiplier, Is.EqualTo(1));
            Assert.That(trap.RemainingUses, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator TrapUniqueUsesRefreshAndWrongPathRejected()
        {
            game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(0, 0));
            game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(1, 0));
            var trap = game.Traps[0];
            var second = game.Traps[1];
            var enemy = Controlled(game.Settings.runner, 0);
            Assert.That(trap.TryActivate(enemy), Is.True);
            Assert.That(trap.TryActivate(enemy), Is.False);
            yield return Steps(50);
            Assert.That(second.TryActivate(enemy), Is.True);
            Assert.That(enemy.SpeedMultiplier, Is.EqualTo(.5f));
            yield return Steps(60);
            Assert.That(enemy.SpeedMultiplier, Is.EqualTo(.5f));
            // A controlled metadata fixture, without introducing another playable path.
            var wrongTrapObject = new GameObject("Wrong path test trigger");
            wrongTrapObject.transform.SetParent(game.Entities);
            wrongTrapObject.AddComponent<CircleCollider2D>().isTrigger = true;
            var wrongTrap = wrongTrapObject.AddComponent<WireTrap>();
            wrongTrap.Initialize(game, 99, 0);
            Assert.That(wrongTrap.TryActivate(enemy), Is.False);
            wrongTrap.Initialize(game, 1, 99);
            Assert.That(wrongTrap.TryActivate(enemy), Is.False);
            Assert.That(wrongTrap.RemainingUses, Is.EqualTo(6));
            for (int i = 0; i < 5; i++)
            {
                var distinct = game.Spawner.Spawn(game.Settings.runner, 0);
                Assert.That(trap.TryActivate(distinct), Is.True);
                Assert.That(distinct.SpeedMultiplier, Is.EqualTo(.5f));
            }
            Assert.That(game.Traps.Contains(trap), Is.False);
            Assert.That(trap.RemainingUses, Is.Zero);
        }

        [UnityTest]
        public IEnumerator WispWarnsRetreatsAndRemainsVulnerableToPhysicalHit()
        {
            var enemy = Controlled(game.Settings.wisp, 5);
            yield return Steps(2);
            Projectile.Launch(game, enemy.Position + Vector2.right * 4.5f, Vector2.left, game.Settings.bow);
            float timeout = Time.time + .4f;
            while (enemy.State != EnemyState.Alert && Time.time < timeout) yield return new WaitForFixedUpdate();
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Alert));
            Assert.That(enemy.Charges, Is.EqualTo(2));
            float warnedAt = game.SimulationTime;
            while (enemy.State == EnemyState.Alert) yield return new WaitForFixedUpdate();
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Evade));
            Assert.That(game.SimulationTime - warnedAt, Is.EqualTo(.2f).Within(.025f));
            Assert.That(enemy.Charges, Is.EqualTo(1));
            Assert.That(enemy.Velocity.x, Is.EqualTo(-1.56f).Within(.01f));
            // This shot approaches from behind, so reverse motion cannot grant immunity.
            float hp = enemy.Health;
            Projectile.Launch(game, enemy.Position + Vector2.left * .7f, Vector2.right, game.Settings.bow);
            yield return Steps(4);
            Assert.That(enemy.Health, Is.LessThan(hp));
            Assert.That(game.EvadeStarts, Is.EqualTo(1));
            Assert.That(enemy.EvadeDistanceTravelled, Is.LessThanOrEqualTo(1.5001f));
            TestContext.WriteLine($"Alert duration {game.SimulationTime - warnedAt:F2}s including post-alert hit; retreating enemy HP {enemy.Health}.");
        }

        [UnityTest]
        public IEnumerator VanishedThreatCancelsWithoutChargeAndRepeatedPairCannotAlertAgain()
        {
            var enemy = Controlled(game.Settings.wisp, 5);
            yield return Steps(2);
            var shot = Projectile.Launch(game, enemy.Position + Vector2.right * 4.5f, Vector2.left, game.Settings.bow);
            float timeout = Time.time + .5f;
            while (enemy.State != EnemyState.Alert && Time.time < timeout) yield return new WaitForFixedUpdate();
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Alert));
            Object.Destroy(shot.gameObject);
            yield return Steps(13);
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Advance));
            Assert.That(enemy.Charges, Is.EqualTo(2));
            Assert.That(enemy.CooldownRemaining, Is.Zero);
            Assert.That(game.Cancellations, Is.EqualTo(1));
            Assert.That(game.Alerts, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AttackBaseWispNeverDodgesAndTargetingPrioritizesAttacker()
        {
            var attacker = Controlled(game.Settings.wisp, game.Path.Length);
            var moving = game.Spawner.Spawn(game.Settings.runner, 11.5f);
            game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 1.5f));
            yield return Steps(1);
            Assert.That(attacker.State, Is.EqualTo(EnemyState.AttackBase));
            Assert.That(game.Towers[0].FindTarget(), Is.EqualTo(attacker));
            Projectile.Launch(game, attacker.Position + Vector2.right * 3, Vector2.left, game.Settings.bow);
            yield return Steps(20);
            Assert.That(attacker.Charges, Is.EqualTo(2));
            Assert.That(attacker.State, Is.EqualTo(EnemyState.AttackBase));
            Assert.That(game.EvadeStarts, Is.Zero);
        }

        [UnityTest]
        public IEnumerator NoDefenseLosesWithSixSeparateAttackersAndRestartResets()
        {
            Time.timeScale = 20;
            game.StartWave();
            float timeout = Time.time + 60;
            while (!game.IsTerminal && Time.time < timeout) yield return null;
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Defeat));
            Assert.That(game.RequiredBases[0].Health, Is.Zero);
            Assert.That(game.Enemies.Count, Is.EqualTo(6));
            foreach (var enemy in game.Enemies)
                TestContext.WriteLine($"Loss: {enemy.State}, position {enemy.Position:F4}, remaining {enemy.RemainingPathDistance:F4}");
            for (int i = 0; i < game.Enemies.Count; i++)
            {
                Assert.That(game.Enemies[i].State, Is.EqualTo(EnemyState.AttackBase));
                Assert.That(game.RequiredBases[0].IsTouching(game.Enemies[i].Position, game.Enemies[i].Data.hitRadius), Is.True);
                var contact = Physics2D.Distance(game.Enemies[i].GetComponent<Collider2D>(), game.RequiredBases[0].GetComponent<Collider2D>());
                Assert.That(contact.distance, Is.LessThanOrEqualTo(.003f), "An attacker must touch the actual base collider.");
                for (int j = i + 1; j < game.Enemies.Count; j++)
                    Assert.That(Vector2.Distance(game.Enemies[i].Position, game.Enemies[j].Position), Is.GreaterThanOrEqualTo(.549f));
            }
            int balance = game.Resources.Energy;
            yield return Steps(10);
            Assert.That(game.Resources.Energy, Is.EqualTo(balance));
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 1.5f)), Is.False);
            Time.timeScale = 1;
            game.Restart();
            yield return null; yield return null;
            var fresh = Object.FindAnyObjectByType<GameManager>();
            Assert.That(fresh.Phase, Is.EqualTo(MatchPhase.Preparation));
            Assert.That(fresh.RequiredBases[0].Health, Is.EqualTo(20));
            Assert.That(fresh.Resources.Energy, Is.EqualTo(150));
            Assert.That(fresh.Enemies.Count + fresh.Projectiles.Count + fresh.Towers.Count + fresh.Traps.Count, Is.Zero);
            Assert.That(fresh.EvadeStarts + fresh.Hits + fresh.Kills + fresh.Spawner.Spawned, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TwoBowsAndTrapWinWithPhysicsHitsAndObservedEvasion()
        {
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(-1, 1.5f)), Is.True);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 1.5f)), Is.True);
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(0, 0)), Is.True);
            game.StartWave();
            Time.timeScale = 20;
            float timeout = Time.time + 90;
            while (!game.IsTerminal && Time.time < timeout) yield return null;
            TestContext.WriteLine($"Result {game.Phase}; HP {game.RequiredBases[0].Health}; energy {game.Resources.Energy}; kills {game.Kills}; hits {game.Hits}; alerts {game.Alerts}; evade starts {game.EvadeStarts}; sim time {game.SimulationTime:F2}.");
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Victory));
            Assert.That(game.RequiredBases[0].Health, Is.GreaterThan(0));
            Assert.That(game.Spawner.Spawned, Is.EqualTo(6));
            Assert.That(game.Kills, Is.EqualTo(6));
            Assert.That(game.Hits, Is.GreaterThanOrEqualTo(27));
            Assert.That(game.EvadeStarts, Is.GreaterThan(0));
            Assert.That(game.WispsThatRetreated.Count, Is.EqualTo(3), "Each authored Wisp should get a retreat attempt in this setup.");
        }

        [UnityTest]
        public IEnumerator BlockedRetreatCancelsAndSameShotNeverWarnsAgain()
        {
            var enemy = Controlled(game.Settings.wisp, 5);
            game.Spawner.Spawn(game.Settings.runner, 4.45f);
            yield return Steps(2);
            var shot = Projectile.Launch(game, enemy.Position + Vector2.right * 4.5f, Vector2.left, game.Settings.bow);
            int id = shot.Id;
            yield return Steps(30);
            Assert.That(game.Alerts, Is.EqualTo(1));
            Assert.That(game.Cancellations, Is.EqualTo(1));
            Assert.That(game.EvadeStarts, Is.Zero);
            Assert.That(enemy.Charges, Is.EqualTo(2));
            Assert.That(enemy.CooldownRemaining, Is.Zero);
            Assert.That(enemy.AlertedProjectileIds.Contains(id), Is.True);
            Assert.That(enemy.Progress - game.Enemies[1].Progress, Is.GreaterThanOrEqualTo(.549f));
        }

        [UnityTest]
        public IEnumerator WispHasTwoFiniteChargesAndFourSecondCooldown()
        {
            var enemy = Controlled(game.Settings.wisp, 2);
            yield return Steps(2);
            float firstStart = 0, secondStart = 0;
            for (int i = 0; i < 2; i++)
            {
                Projectile.Launch(game, enemy.Position + Vector2.right * 4.5f, Vector2.left, game.Settings.bow);
                float timeout = game.SimulationTime + 1;
                while (game.EvadeStarts <= i && game.SimulationTime < timeout) yield return new WaitForFixedUpdate();
                Assert.That(game.EvadeStarts, Is.EqualTo(i + 1));
                if (i == 0) firstStart = game.SimulationTime; else secondStart = game.SimulationTime;
                Assert.That(enemy.Charges, Is.EqualTo(1 - i));
                yield return Steps(210);
                Assert.That(enemy.EvadeDistanceTravelled, Is.LessThanOrEqualTo(1.5001f));
            }
            Assert.That(secondStart - firstStart, Is.GreaterThanOrEqualTo(4));
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Advance));
            Projectile.Launch(game, enemy.Position + Vector2.right * 4.5f, Vector2.left, game.Settings.bow);
            yield return Steps(30);
            Assert.That(game.Alerts, Is.EqualTo(2));
            Assert.That(game.EvadeStarts, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PendingSpawnsOrLivingAttackerPreventClearAndDefeatWins()
        {
            game.StartWave();
            yield return Steps(1);
            Assert.That(game.Spawner.Spawned, Is.EqualTo(1));
            game.Enemies[0].TakeDamage(1000);
            game.EvaluateResolution();
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Combat));
            game.Settings.waves = null;
            var attacker = game.Spawner.Spawn(game.Settings.runner, game.Path.Length);
            yield return Steps(1);
            game.EvaluateResolution();
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Combat));
            game.RequiredBases[0].TakeDamage(20);
            attacker.TakeDamage(1000);
            game.EvaluateResolution();
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Defeat));
            Assert.That(game.Kills, Is.EqualTo(1)); // No post-terminal bounty/kill event.
        }

        [UnityTest]
        public IEnumerator InputSystemKeyboardMousePlaceCancelAndStart()
        {
            var originalBackground = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            var originalEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
                yield return null; yield return null;
                Assert.That(game.Placement.Choice, Is.EqualTo(BuildChoice.ThornBow));
                Vector2 screen = game.WorldCamera.WorldToScreenPoint(new Vector2(5, 1.5f));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                Assert.That(game.Towers.Count, Is.EqualTo(1));
                Assert.That(game.Resources.Energy, Is.EqualTo(100));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                Assert.That(game.Placement.Choice, Is.EqualTo(BuildChoice.None));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null; yield return null;
                Assert.That(game.Phase, Is.EqualTo(MatchPhase.Combat));
                Assert.That(game.Resources.Energy, Is.EqualTo(105));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = originalBackground;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorBehavior;
#endif
            }
        }

        [UnityTest]
        public IEnumerator FormerRoadAttackSlotsCannotDamageBaseBeforeContact()
        {
            var enemy = Controlled(game.Settings.runner, 11.25f);
            yield return Steps(60);
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Advance));
            Assert.That(game.RequiredBases[0].IsTouching(enemy.Position, enemy.Data.hitRadius), Is.False);
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(20));
            float timeout = game.SimulationTime + 5;
            while (enemy.State != EnemyState.AttackBase && game.SimulationTime < timeout)
                yield return new WaitForFixedUpdate();
            Assert.That(enemy.State, Is.EqualTo(EnemyState.AttackBase));
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(20));
            yield return Steps(1);
            var contact = Physics2D.Distance(enemy.GetComponent<Collider2D>(), game.RequiredBases[0].GetComponent<Collider2D>());
            Assert.That(contact.distance, Is.LessThanOrEqualTo(.003f));
            yield return Steps(51);
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(19));
        }

        [UnityTest]
        public IEnumerator BaseContactApproachRejectsTowerPlacement()
        {
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(6, 1.2f)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(7.6f, 1.4f)), Is.False);
            Assert.That(game.Resources.Energy, Is.EqualTo(150));
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 1.5f)), Is.True);
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(4, 0)), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllSixEnemiesCanReachSeparateBaseContactsWithoutJamming()
        {
            // Extra HP is a fixture only, allowing every approach to finish even
            // if the normal 20-HP match would have already ended in defeat.
            game.RequiredBases[0].maxHealth = 1000;
            game.RequiredBases[0].ResetBase();
            game.StartWave();
            Time.timeScale = 20;
            float deadline = game.SimulationTime + 45;
            while (game.SimulationTime < deadline &&
                (!game.Spawner.Finished || game.Enemies.FindAll(e => e.State == EnemyState.AttackBase).Count < 6))
                yield return null;
            yield return Steps(1);
            Assert.That(game.Enemies.Count, Is.EqualTo(6));
            foreach (var enemy in game.Enemies)
            {
                TestContext.WriteLine($"Contact fixture: {enemy.State}, position {enemy.Position:F4}, remaining {enemy.RemainingPathDistance:F4}");
                Assert.That(enemy.State, Is.EqualTo(EnemyState.AttackBase));
                Assert.That(Physics2D.Distance(enemy.GetComponent<Collider2D>(), game.RequiredBases[0].GetComponent<Collider2D>()).distance,
                    Is.LessThanOrEqualTo(.003f));
            }
        }

        [UnityTest]
        public IEnumerator RetreatSpaceSubtractsBothRadiiAndSpacingBuffer()
        {
            var wisp = Controlled(game.Settings.wisp, 5);
            var smallFollower = game.Spawner.Spawn(game.Settings.runner, 4.15f);
            Assert.That(game.Path.AvailableRetreatDistance(wisp, 1.5f), Is.EqualTo(.3f).Within(.0001f));
            smallFollower.TakeDamage(1000);
            var large = FixtureEnemy(game.Settings.runner, .45f, 1.3f);
            game.Spawner.Spawn(large, 4.15f);
            Assert.That(game.Path.AvailableRetreatDistance(wisp, 1.5f), Is.EqualTo(.1f).Within(.0001f));
            game.Settings.safetyMargin = .9f;
            Assert.That(game.Path.AvailableRetreatDistance(wisp, 1.5f), Is.EqualTo(.1f).Within(.0001f), "CPA margin must not change spacing.");
            game.Settings.spacingBuffer = .15f;
            Assert.That(game.Path.AvailableRetreatDistance(wisp, 1.5f), Is.EqualTo(0).Within(.0001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RetreatSpaceRespectsSpawnBoundaryAndMaximumBackstep()
        {
            var nearSpawn = Controlled(game.Settings.wisp, .2f);
            Assert.That(game.Path.AvailableRetreatDistance(nearSpawn, 1.5f), Is.EqualTo(.2f).Within(.0001f));
            var farther = game.Spawner.Spawn(game.Settings.wisp, 5);
            Assert.That(game.Path.AvailableRetreatDistance(farther, .7f), Is.EqualTo(.7f).Within(.0001f));
            Assert.That(game.Path.AvailableRetreatDistance(farther, 1.5f), Is.EqualTo(1.5f).Within(.0001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RetreatClearanceFollowsTheApproachCornerAndThenTheLane()
        {
            var wisp = Controlled(game.Settings.wisp, game.Path.Length);
            Assert.That(game.Path.AvailableRetreatDistance(wisp, 3), Is.EqualTo(3).Within(.0001f));
            var follower = game.Spawner.Spawn(game.Settings.runner, 12.5f);
            float expected = wisp.Progress - (follower.Progress + .55f);
            Assert.That(game.Path.AvailableRetreatDistance(wisp, 3), Is.EqualTo(expected).Within(.0001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExactlyPointThreeAvailableRetreatCanStartEvade()
        {
            var wisp = Controlled(game.Settings.wisp, 5);
            var follower = FixtureEnemy(game.Settings.runner, .25f, 1.3f);
            game.Spawner.Spawn(follower, 4.15f);
            Projectile.Launch(game, wisp.Position + Vector2.right * 4.5f, Vector2.left, game.Settings.bow);
            yield return Steps(20);
            Assert.That(game.EvadeStarts, Is.EqualTo(1));
            Assert.That(wisp.Charges, Is.EqualTo(1));
            Assert.That(wisp.CooldownRemaining, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator LargerFollowerCancelsAlertWithoutChargeOrCooldown()
        {
            var wisp = Controlled(game.Settings.wisp, 5);
            var follower = FixtureEnemy(game.Settings.runner, .45f, 1.3f);
            game.Spawner.Spawn(follower, 4.15f);
            Projectile.Launch(game, wisp.Position + Vector2.right * 4.5f, Vector2.left, game.Settings.bow);
            yield return Steps(20);
            Assert.That(game.Cancellations, Is.EqualTo(1));
            Assert.That(game.EvadeStarts, Is.Zero);
            Assert.That(wisp.Charges, Is.EqualTo(2));
            Assert.That(wisp.CooldownRemaining, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SpawnerWaitsForTheNextEnemyHitboxClearance()
        {
            var large = FixtureEnemy(game.Settings.runner, .45f, 1.6f);
            game.Settings.waves = new[] { new WaveDefinition("Spawn clearance fixture", new WaveSpawn(large, 0)) };
            game.Spawner.Spawn(game.Settings.runner, .6f);
            game.StartWave();
            game.Spawner.Tick(.02f);
            Assert.That(game.Spawner.Spawned, Is.Zero);
            yield return Steps(10);
            Assert.That(game.Spawner.Spawned, Is.EqualTo(1));
            Assert.That(game.Enemies[1].GetComponent<CircleCollider2D>().radius, Is.EqualTo(.45f));
            Assert.That(Vector2.Distance(game.Enemies[0].Position, game.Enemies[1].Position), Is.GreaterThanOrEqualTo(.749f));
        }

        [UnityTest]
        public IEnumerator BowCanTargetEvadingEnemyAndLeadItsReverseVelocity()
        {
            var wisp = Controlled(game.Settings.wisp, 5);
            Projectile.Launch(game, wisp.Position + Vector2.right * 4.5f, Vector2.left, game.Settings.bow);
            float deadline = game.SimulationTime + 1;
            while (wisp.State != EnemyState.Evade && game.SimulationTime < deadline) yield return new WaitForFixedUpdate();
            Assert.That(wisp.State, Is.EqualTo(EnemyState.Evade));
            TowerBuildPad.Create(game.transform, wisp.Position + Vector2.down * 1.5f, game.Settings.spriteMaterial, "Fixture bow pad");
            game.Placement.RefreshBuildPads();
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, wisp.Position + Vector2.down * 1.5f), Is.True);
            var tower = game.Towers[0];
            Assert.That(tower.FindTarget(), Is.EqualTo(wisp));
            float t = Vector2.Distance(tower.Position, wisp.Position) / tower.Data.projectileSpeed;
            Vector2 aim = wisp.Position + wisp.Velocity * t;
            t = Vector2.Distance(tower.Position, aim) / tower.Data.projectileSpeed;
            Vector2 expectedDirection = (wisp.Position + wisp.Velocity * t - tower.Position).normalized;
            tower.Tick(0);
            var shot = game.Projectiles[game.Projectiles.Count - 1];
            Vector2 releasedVelocity = shot.Velocity;
            Assert.That(Vector2.Distance(shot.Velocity.normalized, expectedDirection), Is.LessThan(.0001f));
            yield return Steps(2);
            Assert.That(Vector2.Distance(shot.Velocity, releasedVelocity), Is.LessThan(.0001f));
        }

        [UnityTest]
        public IEnumerator SameBowShotHitsWithoutEvasionButMissesWithRetreat()
        {
            for (int trial = 0; trial < 2; trial++)
            {
                if (trial > 0)
                {
                    game.gameObject.SetActive(false);
                    Object.Destroy(clonedSettings);
                    yield return SceneManager.LoadSceneAsync("Assets/Scenes/Levels/Level1.unity", LoadSceneMode.Single);
                    game = Object.FindAnyObjectByType<GameManager>();
                    clonedSettings = Object.Instantiate(game.Settings);
                    game.Settings = clonedSettings;
                    game.Stage = null;
                    game.GetComponent<CampaignHUD>().enabled = false;
                }
                var data = FixtureEnemy(game.Settings.wisp, .25f, 1.3f);
                data.canEvade = trial == 1;
                var enemy = Controlled(data, 5);
                yield return Steps(2);
                // Preserve the paired-shot fixture's geometry in both trials.
                // Its tower is intentionally beyond the campaign's build edge.
                game.Path.buildBounds = new Rect(-9, -3.5f, 18, 7);
                TowerBuildPad.Create(game.transform, new Vector2(-3, 3), game.Settings.spriteMaterial, "Paired shot fixture pad");
                game.Placement.RefreshBuildPads();
                Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(-3, 3)), Is.True);
                game.Towers[0].Tick(0);
                var shot = game.Projectiles[0];
                Vector2 heading = shot.Velocity;
                yield return Steps(10);
                Assert.That(Vector2.Distance(shot.Velocity, heading), Is.LessThan(.0001f));
                yield return Steps(25); // Observe one shot only; the bow interval is 0.8 s.
                TestContext.WriteLine($"Paired bow test: evasion={data.canEvade}, HP={enemy.Health}, hits={game.Hits}, evade starts={game.EvadeStarts}.");
                Assert.That(enemy.Health, Is.EqualTo(trial == 0 ? 43 : 55));
                Assert.That(game.Hits, Is.EqualTo(trial == 0 ? 1 : 0));
                Assert.That(game.EvadeStarts, Is.EqualTo(trial == 0 ? 0 : 1));
            }
        }
    }
}
