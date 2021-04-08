namespace NotifyPropertyChangedApp.Unity
{
  using global::Unity.Builder;
  using global::Unity.Extension;

  public class NotifyPropertyChangedExt : UnityContainerExtension
  {
    protected override void Initialize()
    {
      Context.Strategies.Add(new NotifyPropertyChangedImplStrategy(), UnityBuildStage.TypeMapping);
    }
  }
}