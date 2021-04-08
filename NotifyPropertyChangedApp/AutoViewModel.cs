namespace NotifyPropertyChangedApp
{
  using System;
  using global::Unity;
  using NotifyPropertyChangedApp.Core;
  using NotifyPropertyChangedApp.Unity;

  public class AutoViewModel : IAutoNotifyPropertyChanged
  { 
    public virtual string Name { get; set; }
    
    public virtual int Age { get; set; }

    [InjectionMethod]
    public void Init()
    {
      this.HookPropertyChanged((s, e) =>
      {
        Console.WriteLine("This is also called");
      });
    }
  }
}