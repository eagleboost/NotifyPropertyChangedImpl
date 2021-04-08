namespace NotifyPropertyChangedApp.Unity
{
  using global::Unity.Builder;
  using global::Unity.Strategies;
  using NotifyPropertyChangedApp.Core;

  public class NotifyPropertyChangedImplStrategy : BuilderStrategy
  {
    public override void PreBuildUp(ref BuilderContext context)
    {
      base.PreBuildUp(ref context);

      if (context.Type.IsSubclassOf<IAutoNotifyPropertyChanged>())
      {
        var implType = NotifyPropertyChangedImpl.GetType(context.Type);
        context.Type = implType; 
      }
    }
  }
}