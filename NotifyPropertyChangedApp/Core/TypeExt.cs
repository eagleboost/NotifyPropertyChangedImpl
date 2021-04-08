namespace NotifyPropertyChangedApp.Core
{
  using System;

  public static class TypeExt
  {
    public static bool IsSubclassOf<T>(this Type type)
    {
      return typeof(T).IsAssignableFrom(type);
    }
  }
}