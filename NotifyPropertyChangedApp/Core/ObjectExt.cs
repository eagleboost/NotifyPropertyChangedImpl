namespace NotifyPropertyChangedApp.Core
{
  using System;
  using System.ComponentModel;

  public static class ObjectExt
  {
    public static void HookPropertyChanged(this object obj, PropertyChangedEventHandler handler)
    {
      if (obj is INotifyPropertyChanged npc)
      {
        npc.PropertyChanged += handler;
      }
    }
  }
}