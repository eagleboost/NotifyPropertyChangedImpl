namespace NotifyPropertyChangedApp.Unity
{
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.ComponentModel;
  using System.Linq;
  using System.Reflection;
  using System.Reflection.Emit;
  using System.Runtime.CompilerServices;
  using NotifyPropertyChangedApp.Core;

  /// <summary>
  /// Inspired by https://grahammurray.wordpress.com/2010/04/13/dynamically-generating-types-to-implement-inotifypropertychanged/
  /// </summary>
  public class NotifyPropertyChangedImpl
  {
    private static readonly MethodAttributes DefaultMethodAttributes =
      MethodAttributes.Public |
      MethodAttributes.SpecialName |
      MethodAttributes.NewSlot |
      MethodAttributes.HideBySig |
      MethodAttributes.Virtual |
      MethodAttributes.Final;
    
    private static readonly MethodImplAttributes EventMethodImplFlags = MethodImplAttributes.Managed | MethodImplAttributes.Synchronized;

    private static readonly Type EventHandlerType = typeof(PropertyChangedEventHandler);
    
    private static readonly MethodInfo AddPropertyChangedMethod = typeof(INotifyPropertyChanged).GetMethod("add_PropertyChanged");
    
    private static readonly MethodInfo RemovePropertyChangedMethod = typeof(INotifyPropertyChanged).GetMethod("remove_PropertyChanged");

    private static readonly ConstructorInfo ArgsCtor = typeof(PropertyChangedEventArgs).GetConstructor(new[] {typeof(string)});
    
    private static readonly ConcurrentDictionary<Type, Type> TypeMap = new ConcurrentDictionary<Type, Type>();

    public static Type GetType(Type baseType)
    {
      if (!baseType.IsSubclassOf<IAutoNotifyPropertyChanged>())
      {
        throw new InvalidOperationException();
      }

      return TypeMap.GetOrAdd(baseType, CreateImplType);
    }
    
    private static Type CreateImplType(Type baseType)
    {
      var assemblyName = new AssemblyName {Name = "<NotifyPropertyChanged>_Assembly"};
      var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
      var moduleBuilder = assemblyBuilder.DefineDynamicModule("<NotifyPropertyChanged>_Module");
      var typeName = GetImplTypeName(baseType);
      var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public, baseType, new [] {typeof(INotifyPropertyChanged)});

      var eventFieldBuilder = EmitPropertyChangedField(typeBuilder);
      var notifyChangeBuilder = EmitNotifyPropertyChanged(typeBuilder, eventFieldBuilder);

      var properties = GetTargetProperties(baseType);
      foreach (var property in properties)
      {
        EmitPropertySetter(property, notifyChangeBuilder, typeBuilder);
      }
      
      return typeBuilder.CreateType();
    }

    private static FieldBuilder EmitPropertyChangedField(TypeBuilder tb)
    {
      var eventFieldBuilder = tb.DefineField("PropertyChanged", EventHandlerType, FieldAttributes.Public);
      var eventBuilder = tb.DefineEvent("PropertyChanged", EventAttributes.None, EventHandlerType);
      
      eventBuilder.SetAddOnMethod(CreateAddMethod(tb, eventFieldBuilder));
      eventBuilder.SetRemoveOnMethod(CreateRemoveMethod(tb, eventFieldBuilder));

      return eventFieldBuilder;
    }

    private static MethodBuilder CreateAddMethod(TypeBuilder tb, FieldBuilder eventFieldBuilder)
    {
      return CreateAddRemoveMethodCore(tb, eventFieldBuilder, AddPropertyChangedMethod);
    }

    private static MethodBuilder CreateRemoveMethod(TypeBuilder tb, FieldBuilder eventFieldBuilder)
    {
      return CreateAddRemoveMethodCore(tb, eventFieldBuilder, RemovePropertyChangedMethod);
    }

    private static MethodBuilder CreateAddRemoveMethodCore(TypeBuilder typeBuilder, FieldBuilder eventFieldBuilder, MethodInfo addRemoveMethod)
    {
      var methodName = addRemoveMethod.Name;
       var method = typeBuilder.DefineMethod(methodName, DefaultMethodAttributes, null, new[] { EventHandlerType });
       method.SetImplementationFlags(EventMethodImplFlags);
 
      var ilGen = method.GetILGenerator();

      var delegateAction = addRemoveMethod == AddPropertyChangedMethod ? "Combine" : "Remove";
      var dlMethod = typeof(Delegate).GetMethod(delegateAction, new[] {typeof(Delegate), typeof(Delegate)});
      
      //// PropertyChanged += value;
      //// PropertyChanged -= value;
      ilGen.Emit(OpCodes.Ldarg_0);
      ilGen.Emit(OpCodes.Ldarg_0);
      ilGen.Emit(OpCodes.Ldfld, eventFieldBuilder);
      ilGen.Emit(OpCodes.Ldarg_1);
      ilGen.EmitCall(OpCodes.Call, dlMethod, null);
      ilGen.Emit(OpCodes.Castclass, EventHandlerType);
      ilGen.Emit(OpCodes.Stfld, eventFieldBuilder);
      ilGen.Emit(OpCodes.Ret);
 
      typeBuilder.DefineMethodOverride(method, addRemoveMethod);
 
      return method;
    }
    
    private static MethodBuilder EmitNotifyPropertyChanged(TypeBuilder typeBuilder, FieldBuilder eventFieldBuilder)
    {
      var methodBuilder = typeBuilder.DefineMethod("NotifyPropertyChanged", MethodAttributes.Family | MethodAttributes.Virtual, null, new Type[] { typeof(string) });
 
      var methodIl = methodBuilder.GetILGenerator();
      
      var labelExit = methodIl.DefineLabel();
 
      // if (PropertyChanged == null)
      // {
      //      return;
      // }
      methodIl.Emit(OpCodes.Ldarg_0);
      methodIl.Emit(OpCodes.Ldfld, eventFieldBuilder);
      methodIl.Emit(OpCodes.Ldnull);
      methodIl.Emit(OpCodes.Ceq);
      methodIl.Emit(OpCodes.Brtrue, labelExit);
 
      // this.PropertyChanged(this,
      // new PropertyChangedEventArgs(propertyName));
      methodIl.Emit(OpCodes.Ldarg_0);
      methodIl.Emit(OpCodes.Ldfld, eventFieldBuilder);
      methodIl.Emit(OpCodes.Ldarg_0);
      methodIl.Emit(OpCodes.Ldarg_1);
      methodIl.Emit(OpCodes.Newobj, ArgsCtor);
      methodIl.EmitCall(OpCodes.Callvirt, EventHandlerType.GetMethod("Invoke"), null);
 
      // return;
      methodIl.MarkLabel(labelExit);
      methodIl.Emit(OpCodes.Ret);
 
      return methodBuilder;
    }
    
    private static void EmitPropertySetter(PropertyInfo item, MethodBuilder raisePropertyChanged, TypeBuilder typeBuilder)
    {
      var setMethod = item.GetSetMethod();
 
      //get an array of the parameter types.
      var types = setMethod.GetParameters().Select(t => t.ParameterType);
 
      var setMethodBuilder = typeBuilder.DefineMethod(setMethod.Name, setMethod.Attributes, setMethod.ReturnType, types.ToArray());
      typeBuilder.DefineMethodOverride(setMethodBuilder, setMethod); 
      
      var setMethodWrapperIl = setMethodBuilder.GetILGenerator();
 
      // base.[PropertyName] = value;
      setMethodWrapperIl.Emit(OpCodes.Ldarg_0);
      setMethodWrapperIl.Emit(OpCodes.Ldarg_1);
      setMethodWrapperIl.EmitCall(OpCodes.Call, setMethod, null);
 
      // RaisePropertyChanged("[PropertyName]");
      setMethodWrapperIl.Emit(OpCodes.Ldarg_0);
      setMethodWrapperIl.Emit(OpCodes.Ldstr, item.Name);
      setMethodWrapperIl.EmitCall(OpCodes.Call, raisePropertyChanged, null);
 
      // return;
      setMethodWrapperIl.Emit(OpCodes.Ret);
    }
    
    private static string GetImplTypeName(Type type)
    {
      return $"<NotifyPropertyChanged>_Impl_{type.Name}";
    }

    /// <summary>
    /// Get the properties need to be override with Property<> as backing field
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static IEnumerable<PropertyInfo> GetTargetProperties(Type type)
    {
      var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
      return type.GetProperties().Where(t => IsTargetProperty(t, fields));
    }

    /// <summary>
    /// 1. Make sure it's auto property, i.e. no method body for getter and setter
    /// 2. Make sure it does not have the [Dependency] attribute
    /// 3. Make sure it's not the PropertyStore property 
    /// </summary>
    /// <param name="property"></param>
    /// <param name="fields"></param>
    /// <returns></returns>
    private static bool IsTargetProperty(PropertyInfo property, IReadOnlyCollection<FieldInfo> fields)
    {
      var isCompilerGenerated = IsCompilerGenerated(property.GetGetMethod());
      if (!isCompilerGenerated)
      {
        return false;
      }

      if (IsDependency(property))
      {
        return false;
      }

      var propertyName = property.Name;
      foreach (var field in fields)
      {
        ////the name of the backing field for auto property is something like <PropertyName>k__BackingField
        if (field.Name.Contains(propertyName) && field.Name.Contains("BackingField"))
        {
          if (IsCompilerGenerated(field))
          {
            return true;
          }
        }
      }

      return false;
    }

    private static bool IsCompilerGenerated(MemberInfo memberInfo)
    {
      var result = memberInfo.GetCustomAttributes<CompilerGeneratedAttribute>(true).Any();
      return result;
    }

    private static bool IsDependency(PropertyInfo property)
    {
      var result = property.GetCustomAttributes<DependencyAttribute>(true).Any();
      return result;
    }
  }
}