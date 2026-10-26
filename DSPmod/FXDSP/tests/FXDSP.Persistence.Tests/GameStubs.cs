using System;

// Minimal host surface only. Tests execute the real Core and MarketSession sources.
internal sealed class GameDesc
{
    internal DateTime creationTime;
    internal int galaxySeed;
}

internal sealed class GameData
{
    internal GameDesc gameDesc;
}

internal sealed class GameMain
{
    internal static GameMain instance;
    internal static GameData data;
    internal static long gameTick;
    internal static bool isRunning;
    internal static bool isPaused;
    internal bool isMenuDemo;
}
