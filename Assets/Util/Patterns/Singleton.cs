using System;

public class Singleton<T>
{
    public static T Instance { get; protected set; }
}