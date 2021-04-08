namespace NotifyPropertyChangedApp
{
  using System;
  using global::Unity;
  using global::Unity.Interception;
  using global::Unity.Interception.ContainerIntegration;
  using global::Unity.Interception.Interceptors.InstanceInterceptors.InterfaceInterception;
  using NotifyPropertyChangedApp.Core;
  using NotifyPropertyChangedApp.Unity;

  internal class Program
  {
    public static void Main(string[] args)
    {
      var container = new UnityContainer();
      container.AddNewExtension<NotifyPropertyChangedExt>();
      container.AddNewExtension<Interception>();

      var interceptor = new Interceptor<InterfaceInterceptor>();
      var interceptionBehavior = new InterceptionBehavior<NotifyPropertyChangedBehavior>();
      container.RegisterType<IViewModel, ViewModel>(interceptor, interceptionBehavior);

      var viewModel = container.Resolve<IViewModel>();
      viewModel.HookPropertyChanged((s, e) =>
      {
        Console.WriteLine("This is called because the PropertyChanged event handler is attached from out side of the ViewModel");
      });
      viewModel.Name = "1";
      
      var autoViewModel = container.Resolve<AutoViewModel>();
      autoViewModel.HookPropertyChanged((s, e) =>
      {
        Console.WriteLine("This is called");
      });
      autoViewModel.Name = "1";
    }
  }
}