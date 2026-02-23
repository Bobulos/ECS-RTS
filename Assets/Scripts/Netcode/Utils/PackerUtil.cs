using System;
using Unity.Collections;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities.UniversalDelegates;
public struct PackedBittableInput
{
    // 2 bytes
    public ushort Turn;   // 0–65535
    
    //Add back later
    //public sbyte Player;   // signed
    public byte Op;       // action type
    
    public byte BA; // for shiftin
    public byte BB;
    public byte BC;
    public ulong LA;
    public ulong LB;
    public ulong LC;
    public ulong LD;
    public ulong LE;
    public ulong LF;
}
public static class PackerUtil
{
    const float MAX_RAY_LENGTH = 600f;
    private static CollisionFilter TERRAIN_FILTER = new CollisionFilter
    {
        CollidesWith = 1 << 7,
        BelongsTo = CollisionFilter.Default.BelongsTo,
        GroupIndex = 0
    };
    public static PackedBittableInput Pack(
        PhysicsWorldSingleton phys, 
        ushort turn, 
        BittableInput input)
    {
        //things tha need to be done regardless
        var pack = new PackedBittableInput
        {
            Turn = turn,
            Op = (byte)input.Type,
        };

        switch (input.Type)
        {
            //these require no aditional info
            case InputType.None:
                break;
            case InputType.ClearUnits:
                break;
            
            // 25 + 3 B
            case InputType.MoveUnits:
                PackMove(input.Move, ref pack, phys);
                break;
            //dont ask
            case InputType.SelectUnits:
                PackSelect(input.Select, ref pack);
                break;
            // 1 + 3 B
            case InputType.CodeSelectUnits:
                PackCodeSelect(input.CodeSelect, ref pack);
                break;
            //32 + 3B
            case InputType.Action:
                PackAction(input.Action, ref pack, phys);
                break;
            
        }
        return pack;
    }
    public static BittableInput Unpack(
        PackedBittableInput input)
    {
        var un = new BittableInput
        {
            Type = (InputType)input.Op,
            //Add team support
            Team = 0
        };

        //things tha need to be done regardless
        //un.Turn = turn;

        switch (un.Type)
        {
            //these require no aditional info
            case InputType.None:
                break;
            case InputType.ClearUnits:
                break;
            
            // 25 + 3 B
            case InputType.MoveUnits:
                UnpackMove(input, ref un);
                break;
            //dont ask
            case InputType.SelectUnits:
                UnpackSelect(input, ref un);
                break;
            // 1 + 3 B
            case InputType.CodeSelectUnits:
                UnpackCodeSelect(input, ref un);
                break;
            //32 + 3B
            case InputType.Action:
                UnpackAction(input, ref un);
                //UnityEngine.Debug.Log("Unpacking action");
                break;
            
        }
        return un;
    }
    #region  Packers & Unpackers
    #region Select
    
    private static void PackSelect(
        FixedSelectionData d, 
        ref PackedBittableInput pack)
    {
        pack.BA = Convert.ToByte(d.Shifting);

        FixedList64Bytes<float2> corners = new FixedList64Bytes<float2>();
        float3 cam = float3.zero;
        float3 previous = float3.zero;

        //get cam pos
        foreach (float3 vert in d.Value)
        {
            //detected camera position
            if (math.distancesq(vert, previous) < 0.001f)
            {
                cam = vert;
                break;
            }
            previous = vert;
        }
        foreach (float3 vert in d.Value)
        {   
            if (math.distancesq(vert, cam) < 0.001f)
            {
                //detected camera position
            } else
            {
                corners.Add(new float2(vert.x, vert.z));
            }
        }
        //LA - 1/2 of LB is for cam
        pack.LA = Pack2x32(Quantize(cam.x),Quantize(cam.y));
        pack.LB = (ulong)Quantize(cam.z);

        //LC - LF are vert corners
        float2 c = corners[0];
        pack.LC = Pack2x32(Quantize(c.x), Quantize(c.y));
        c = corners[1];
        pack.LD = Pack2x32(Quantize(c.x), Quantize(c.y));
        c = corners[2];
        pack.LE = Pack2x32(Quantize(c.x), Quantize(c.y));
        c = corners[3];
        pack.LF = Pack2x32(Quantize(c.x), Quantize(c.y));
    }
    private static void UnpackSelect(
        PackedBittableInput d,
        ref BittableInput input
    )
    {
        input.Select.Value = new FixedList128Bytes<float3>();
        //initialize the data
        for(int i = 0; i < 8; i ++) { input.Select.Value.Add(float3.zero);}

        input.Select.Shifting = Convert.ToBoolean(d.BA);

        //LC - LF are vert corners add first
        Unpack2x32(d.LC, out int xq, out int zq);
        //de quant
        float x = Dequantize(xq);
        float z = Dequantize(zq);
        input.Select.Value[0] = new float3(x,0,z);

        Unpack2x32(d.LD, out xq, out zq);
        //de quant
        x = Dequantize(xq);
        z = Dequantize(zq);
        input.Select.Value[1] = new float3(x,0,z);

        Unpack2x32(d.LE, out xq, out zq);
        //de quant
        x = Dequantize(xq);
        z = Dequantize(zq);
        input.Select.Value[2] = new float3(x,0,z);

        Unpack2x32(d.LF, out xq, out zq);
        //de quant
        x = Dequantize(xq);
        z = Dequantize(zq);
        input.Select.Value[3] = new float3(x,0,z);

        
       
        //x and y
        Unpack2x32(d.LA, out int xcq, out int ycq);
        int zcq = (int) d.LB;
        float3 unpackedCamPos 
        = new float3(Dequantize(xcq),Dequantize(ycq),Dequantize(zcq));

        // Set appropriate verts
        input.Select.Value[4] = unpackedCamPos;
        input.Select.Value[5] = unpackedCamPos;
        input.Select.Value[6] = unpackedCamPos;
        input.Select.Value[7] = unpackedCamPos;
    }
    #endregion
    // public bool Shifting;
    // public float3 RayOrigin;
    // public float3 RayDirection;

    //needs to pack shifing ray stuff and prefab index 
    #region Action
    private static void PackAction( 
        ActionData d,
        ref PackedBittableInput input,
        PhysicsWorldSingleton phys)
    {
        input.BA = Convert.ToByte(d.Shifting);

        input.BB = d.ActionByte;

        input.BC = (byte)d.Info.ActionType;

        input.LB = (ulong)d.Info.PrefabIndex;

        var raycastInput = new RaycastInput
        {
            Start = d.RayOrigin,
            End = d.RayOrigin + d.RayDirection*MAX_RAY_LENGTH,
            Filter = TERRAIN_FILTER
        };
        if (phys.CastRay(raycastInput, out var hit))
        {
            input.LA = Pack2x32(
                Quantize(hit.Position.x), 
                Quantize(hit.Position.z));
        }
    }
    private static void UnpackAction( 
        PackedBittableInput d,
        ref BittableInput input)
    {
        input.Action.Shifting = Convert.ToBoolean(d.BA);

        input.Action.ActionByte = d.BB;
        input.Action.Info.ActionType = (ActionType)d.BC;
        //UnityEngine.Debug.Log($"Unpacked action type {(ActionType)d.BC}");
        
        input.Action.Info.PrefabIndex = (int)d.LB;

        Unpack2x32(d.LA, out int qx, out int qz);

        float x = Dequantize(qx);
        float z = Dequantize(qz);

        UnityEngine.Debug.DrawRay(new float3(x,40f,z),math.down()*MAX_RAY_LENGTH, UnityEngine.Color.aquamarine, 10f);
        input.Action.RayOrigin = new float3(x,40f,z);
        input.Action.RayDirection = math.down()*MAX_RAY_LENGTH;
        //un.Action = new ActionData { Value = d.LA };
    }
    #endregion
    #region Code Select
    private static void PackCodeSelect(
        byte CodeSelect,
        ref PackedBittableInput input)
    {
        input.BB = CodeSelect;
    }
    private static void UnpackCodeSelect(
        PackedBittableInput d,
        ref BittableInput input)
    {
        input.CodeSelect = d.BB;
    }
    #endregion
    #region Move
    private static void PackMove( 
        MoveUnitsData d,
        ref PackedBittableInput input,
        PhysicsWorldSingleton phys)
    {
        var raycastInput = new RaycastInput
        {
            Start = d.RayOrigin,
            End = d.RayOrigin + d.RayDirection*MAX_RAY_LENGTH,
            Filter = TERRAIN_FILTER
        };
        if (phys.CastRay(raycastInput, out var hit))
        {
            input.BA = Convert.ToByte(d.Shifting);
            input.LA = Pack2x32(
                Quantize(hit.Position.x), 
                Quantize(hit.Position.z));
        }
        // else
        // {
        //     UnityEngine.Debug.LogWarning($"Missed move units ray at turn {input.Turn} pack op fail");
        // }
    }
    private static void UnpackMove( 
        PackedBittableInput d,
        ref BittableInput input)
    {
        input.Move.Shifting = Convert.ToBoolean(d.BA);

        Unpack2x32(d.LA, out int qx, out int qz);

        float x = Dequantize(qx);
        float z = Dequantize(qz);

        input.Move.RayOrigin = new float3(x,40f,z);
        input.Move.RayDirection = math.down();
    }
    #endregion
    #endregion
    
    #region  Helpers


    //pack floats to uints
    const float POS_PRECISION = 4f; // 0.25 meter
    static int Quantize(float v) => (int)math.round(v * POS_PRECISION);
    static float Dequantize(int v) => v / POS_PRECISION;
    static ulong Pack2x32(int x, int z)
    {
        return ((ulong)(uint)x << 32) | (uint)z;
    }
    static void Unpack2x32(ulong packed, out int x, out int z)
    {
        x = (int)(packed >> 32);
        z = (int)(packed & 0xFFFFFFFF);
    }
    #endregion
}