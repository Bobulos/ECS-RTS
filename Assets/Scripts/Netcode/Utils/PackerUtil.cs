using System;
using Unity.Collections;
using Unity.Physics;
using Unity.Mathematics;

public struct PackedBittableInput
{
    // 2 bytes
    public ushort Turn;   // 0–65535
    
    //Add back later
    //public sbyte Player;   // signed
    public byte Op;       // action type
    
    public byte BA;
    public ulong LA;
    public ulong LB;
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

                break;
            // 1 + 3 B
            case InputType.CodeSelectUnits:
                UnpackCodeSelect(input, ref un);
                break;
            //32 + 3B
            case InputType.Action:
                UnpackAction(input, ref un);
                break;
            
        }
        return un;
    }
    #region  Packers & Unpackers
    // public bool Shifting;
    // public float3 RayOrigin;
    // public float3 RayDirection;

    //needs to pack shifing ray stuff and prefab index 
    private static void PackAction( 
        ActionData d,
        ref PackedBittableInput input,
        PhysicsWorldSingleton phys)
    {
        input.BA = Convert.ToByte(d.Shifting);
        input.LB = (ulong)d.Info.PrefabIndex;

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
    }
    private static void UnpackAction( 
        PackedBittableInput d,
        ref BittableInput input)
    {
        input.Action.Shifting = Convert.ToBoolean(d.BA);
        input.Action.Info.PrefabIndex = (int)d.LB;

        Unpack2x32(d.LA, out int qx, out int qz);

        float x = Dequantize(qx);
        float z = Dequantize(qz);

        input.Move.RayOrigin = new float3(x,40f,z);
        input.Move.RayDirection = math.down();
        //un.Action = new ActionData { Value = d.LA };
    }
    private static void PackCodeSelect(
        byte CodeSelect,
        ref PackedBittableInput input)
    {
        input.BA = CodeSelect;
    }
    private static void UnpackCodeSelect(
        PackedBittableInput d,
        ref BittableInput input)
    {
        input.CodeSelect = d.BA;
    }

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