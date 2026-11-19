using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Mechanics;
using Platformer.Model;
using UnityEngine;
using UnityEngine.TestTools;

// ВАРИАНТ 16: PlayerEnteredVictoryZone (PlayMode)
// Проверяется, что вход игрока в VictoryZone планирует событие PlayerEnteredVictoryZone,
// а после выполнения события (Simulation.Tick) управление игроком отключается.
public class VictoryZoneTests
{
    private GameObject playerObj;
    private GameObject zoneObj;
    private GameObject otherObj;

    [SetUp]
    public void SetUp()
    {
        // Очищаем очередь событий симуляции перед каждым тестом
        Simulation.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        if (playerObj != null) Object.Destroy(playerObj);
        if (zoneObj != null) Object.Destroy(zoneObj);
        if (otherObj != null) Object.Destroy(otherObj);
        Simulation.GetModel<PlatformerModel>().player = null;
        Simulation.Clear();
    }

    // PLAYMODE ТЕСТ ПО ВАРИАНТУ 16: вход игрока в зону победы планирует событие
    [UnityTest]
    public IEnumerator PlayMode_PlayerEntersVictoryZone_SchedulesEvent()
    {
        // Создаем игрока. Объект неактивен, чтобы Awake() PlayerController не обращался
        // к Input System — для теста нужен только сам компонент и коллайдер
        playerObj = new GameObject("TestPlayer");
        playerObj.SetActive(false);
        PlayerController player = playerObj.AddComponent<PlayerController>();
        Collider2D playerCollider = playerObj.AddComponent<BoxCollider2D>();

        // Событие при выполнении вызывает model.player.animator.SetTrigger("victory"),
        // поэтому назначаем игроку Animator (поле internal) и регистрируем игрока в модели
        Animator animator = playerObj.AddComponent<Animator>();
        typeof(PlayerController)
            .GetField("animator", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(player, animator);
        Simulation.GetModel<PlatformerModel>().player = player;

        // Создаем зону победы с коллайдером-триггером
        zoneObj = new GameObject("TestVictoryZone");
        BoxCollider2D zoneCollider = zoneObj.AddComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;
        VictoryZone zone = zoneObj.AddComponent<VictoryZone>();

        Assert.AreEqual(0, ScheduledEventsCount(), "Ошибка: очередь событий должна быть пустой на старте!");
        Assert.IsTrue(player.controlEnabled, "Ошибка: управление игроком должно быть включено на старте!");

        // Подписываемся на выполнение события, чтобы проверить, какая зона его вызвала
        bool eventExecuted = false;
        VictoryZone eventZone = null;
        System.Action<PlayerEnteredVictoryZone> handler = ev =>
        {
            eventExecuted = true;
            eventZone = ev.victoryZone;
        };
        PlayerEnteredVictoryZone.OnExecute += handler;

        // Animator без AnimatorController пишет предупреждение в лог — для теста это не ошибка
        LogAssert.ignoreFailingMessages = true;

        try
        {
            // Имитируем вход игрока в триггер: вызываем обработчик OnTriggerEnter2D зоны
            InvokeOnTriggerEnter2D(zone, playerCollider);

            // 1) Событие запланировано в очереди симуляции
            Assert.AreEqual(1, ScheduledEventsCount(),
                "Ошибка: вход игрока в VictoryZone не запланировал событие PlayerEnteredVictoryZone!");

            // 2) После Tick() событие выполняется и ссылается на нашу зону
            Simulation.Tick();
            Assert.IsTrue(eventExecuted, "Ошибка: событие PlayerEnteredVictoryZone не было выполнено!");
            Assert.AreSame(zone, eventZone, "Ошибка: событие ссылается на другую VictoryZone!");

            // 3) Результат события: управление игроком отключено (уровень пройден)
            Assert.IsFalse(player.controlEnabled,
                "Ошибка: после входа в VictoryZone управление игроком не отключилось!");
        }
        finally
        {
            PlayerEnteredVictoryZone.OnExecute -= handler;
        }

        yield return null;
    }

    // Дополнительная проверка: объект без PlayerController не вызывает победу
    [UnityTest]
    public IEnumerator PlayMode_NonPlayerEntersVictoryZone_NoEventScheduled()
    {
        otherObj = new GameObject("TestBox");
        Collider2D otherCollider = otherObj.AddComponent<BoxCollider2D>();

        zoneObj = new GameObject("TestVictoryZone");
        zoneObj.AddComponent<BoxCollider2D>().isTrigger = true;
        VictoryZone zone = zoneObj.AddComponent<VictoryZone>();

        InvokeOnTriggerEnter2D(zone, otherCollider);

        Assert.AreEqual(0, ScheduledEventsCount(),
            "Ошибка: объект без PlayerController не должен вызывать событие победы!");

        yield return null;
    }

    // Вызов приватного обработчика Unity OnTriggerEnter2D у VictoryZone
    private static void InvokeOnTriggerEnter2D(VictoryZone zone, Collider2D collider)
    {
        MethodInfo method = typeof(VictoryZone).GetMethod("OnTriggerEnter2D",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "Ошибка: у VictoryZone не найден метод OnTriggerEnter2D!");
        method.Invoke(zone, new object[] { collider });
    }

    // Количество событий в приватной очереди Simulation.eventQueue
    private static int ScheduledEventsCount()
    {
        FieldInfo field = typeof(Simulation).GetField("eventQueue",
            BindingFlags.NonPublic | BindingFlags.Static);
        object queue = field.GetValue(null);
        return (int)queue.GetType().GetProperty("Count").GetValue(queue);
    }
}
