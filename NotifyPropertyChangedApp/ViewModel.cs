namespace NotifyPropertyChangedApp
{
  using System;
  using global::Unity;
  using NotifyPropertyChangedApp.Core;

  public interface IViewModel
  {
    string Name { get; set; }
    
    int Age { get; set; }
  }
  
  public class ViewModel : IViewModel
  {
    public string Name { get; set; }
    
    public int Age { get; set; }
    
    [InjectionMethod]
    public void Init()
    {
      this.HookPropertyChanged((s, e) =>
      {
        Console.WriteLine("This is not called");
      });
    }
  }
}