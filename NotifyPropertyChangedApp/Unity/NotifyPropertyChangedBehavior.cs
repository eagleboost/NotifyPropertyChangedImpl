namespace NotifyPropertyChangedApp.Unity
{
  using System;
  using System.Collections.Generic;
  using System.ComponentModel;
  using System.Reflection;
  using global::Unity.Interception.InterceptionBehaviors;
  using global::Unity.Interception.PolicyInjection.Pipeline;

  /// <summary>
  /// https://docs.microsoft.com/en-us/previous-versions/msp-n-p/ff660851(v=pandp.20)?redirectedfrom=MSDN#implement-inotifypropertychanged-example
  /// </summary>
  public class NotifyPropertyChangedBehavior : IInterceptionBehavior
  {
    private static readonly MethodInfo addEventMethodInfo = typeof(INotifyPropertyChanged).GetEvent("PropertyChanged").GetAddMethod();

    private static readonly MethodInfo removeEventMethodInfo = typeof(INotifyPropertyChanged).GetEvent("PropertyChanged").GetRemoveMethod();

    /// <summary>
    ///   Implement this method to execute your behavior processing.
    /// </summary>
    /// <param name="input">Inputs to the current call to the target.</param>
    /// <param name="getNext">Delegate to execute to get the next delegate in the behavior chain.</param>
    /// <returns>
    ///   Return value from the target.
    /// </returns>
    public IMethodReturn Invoke(IMethodInvocation input, GetNextInterceptionBehaviorDelegate getNext)
    {
      if (input.MethodBase == addEventMethodInfo)
      {
        return AddEventSubscription(input, getNext);
      }

      if (input.MethodBase == removeEventMethodInfo)
      {
        return RemoveEventSubscription(input, getNext);
      }

      if (IsPropertySetter(input))
      {
        return InterceptPropertySet(input, getNext);
      }
      
      return getNext()(input, getNext);
    }

    /// <summary>
    ///   Optimization hint for proxy generation - will this behavior actually
    ///   perform any operations when invoked?
    /// </summary>
    public bool WillExecute => true;

    /// <summary>
    ///   Returns the interfaces required by the behavior for the objects it intercepts.
    /// </summary>
    /// <returns>
    ///   The required interfaces.
    /// </returns>
    public IEnumerable<Type> GetRequiredInterfaces()
    {
      return new[] {typeof(INotifyPropertyChanged)};
    }

    private event PropertyChangedEventHandler propertyChanged;

    private IMethodReturn AddEventSubscription(IMethodInvocation input, GetNextInterceptionBehaviorDelegate getNext)
    {
      var subscriber = (PropertyChangedEventHandler) input.Arguments[0];
      propertyChanged += subscriber;
      return input.CreateMethodReturn(null);
    }

    private IMethodReturn RemoveEventSubscription(IMethodInvocation input, GetNextInterceptionBehaviorDelegate getNext)
    {
      var subscriber = (PropertyChangedEventHandler) input.Arguments[0];
      propertyChanged -= subscriber;
      return input.CreateMethodReturn(null);
    }

    private static bool IsPropertySetter(IMethodInvocation input)
    {
      return input.MethodBase.IsSpecialName && input.MethodBase.Name.StartsWith("set_");
    }

    private IMethodReturn InterceptPropertySet(IMethodInvocation input, GetNextInterceptionBehaviorDelegate getNext)
    {
      var propertyName = input.MethodBase.Name.Substring(4);

      var returnValue = getNext()(input, getNext);

      var subscribers = propertyChanged;
      subscribers?.Invoke(input.Target, new PropertyChangedEventArgs(propertyName));

      return returnValue;
    }
  }
}