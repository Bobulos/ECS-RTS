using System;
using TMPro;
using Unity.Entities;
using UnityEngine;
using Unity.NetCode;

public class KilledDisplay : MapLoadedAccess
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private float readRate = 1f;
    // Update is called once per frame

    //private EntityManager entityManager;
    private EntityQuery query;
    public override void OnLoad()
    {
        //base.Start();
        var entityManager = ClientServerBootstrap.ClientWorld.EntityManager;
        query = entityManager.CreateEntityQuery(typeof(GameStats));
        InvokeRepeating(nameof(UpdateKilled), readRate, readRate);
    }
    private void UpdateKilled()
    {
        if (!query.TryGetSingleton<GameStats>(out var stats)) return;
        text.text = $"{stats.Killed}";
    }
}
