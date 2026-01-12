using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class InputPlayback : MonoBehaviour
{
    List<InputRecord> record;

    //bridges
    public InputBridge input;
    public ConstructionBridge construction;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    bool playing = false;
    public void StartReplay(string file)
    {
        input = GameObject.FindFirstObjectByType<InputBridge>();
        construction = GameObject.FindFirstObjectByType<ConstructionBridge>();
        playing = true;
        record = InputDecoder.LoadLog(Path.Combine(Application.persistentDataPath, file));
    }

    // Update is called once per frame
    uint step;
    void FixedUpdate()
    {
        step++;
        if (record == null || record.Count == 0 || !playing) return;

        // Process all records that are scheduled for this step OR earlier
        // (The <= handles cases where the game might have hitched)
        while (record.Count > 0 && record[0].Step <= step)
        {
            ProcessRecord(record[0]);
            record.RemoveAt(0); // ONLY remove here
        }
    }
    void ProcessRecord(InputRecord r)
    {
        if (r.Step != step) return;
        switch (r.Type)
        {
            case InputType.SelectUnits:
                //add team
                input.PlaybackInput(r);
                break;
            case InputType.MoveUnits:
                input.PlaybackInput(r);
                break;
            case InputType.ClearUnits:
                input.PlaybackInput(r);
                break;
            case InputType.ConstructWalls:
                //1Debug.Log("BUILD WALLLSLSLSLLSLSLSL");
                construction.PlaybackInput(r);
                break;

        }
    }
}
