using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Client.Impl.Requests;
using TypeIndicatorConverter.Core.Attribute;

namespace Client.Impl;

public static class Generator
{
    public static Type GenerateDTO(MethodInfo method)
    {
        var parameters = method.GetParameters();

        var methodName = new StringBuilder(method.Name);
        methodName.Append("Command_");
        var alias = new StringBuilder(method.Name);
        alias.Append('-');
        foreach (var parameter in parameters)
        {
            methodName.Append(parameter.ParameterType.Name);
            alias.Append(parameter.ParameterType.Name);
        }

        //create the builder
        AssemblyName assembly = typeof(Request).Assembly.GetName();
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assembly, AssemblyBuilderAccess.Run);

        // The module name is usually the same as the assembly name.
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assembly.Name);
        
        //create the class
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            methodName.ToString(),
            TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit,
            typeof(Request)
        );

        var fieldBuilders = new FieldBuilder[parameters.Length];
        var parameterTypes = new Type[parameters.Length];
        var i = 0;
        
        // The property "set" and property "get" methods require a special
        // set of attributes.
        MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

        foreach (var parameter in parameters)
        {
            var parameterName = parameter.Name;
            var parameterType = parameterTypes[i] = parameter.ParameterType;
            
            var propertyName = char.ToUpperInvariant(parameter.Name[0]) + parameter.Name[1..];

            // Add a private field of type int (Int32).
            FieldBuilder fieldBuilder = fieldBuilders[i] = typeBuilder.DefineField(
                $"m_{parameterName}",
                parameterType,
                FieldAttributes.Private);
            i++;
            
            // Define a property named Number that gets and sets the private
            // field.
            //
            // The last argument of DefineProperty is null, because the
            // property has no parameters. (If you don't specify null, you must
            // specify an array of Type objects. For a parameterless property,
            // use the built-in array with no elements: Type.EmptyTypes)

            PropertyBuilder pbNumber = typeBuilder.DefineProperty(
                propertyName,
                PropertyAttributes.HasDefault,
                parameterType,
                null);

           
            // Define the "get" accessor method for Number. The method returns
            // an integer and has no arguments. (Note that null could be
            // used instead of Types.EmptyTypes)
            MethodBuilder mbNumberGetAccessor = typeBuilder.DefineMethod(
                $"get_{propertyName}",
                getSetAttr,
                parameterType,
                Type.EmptyTypes);

            ILGenerator numberGetIL = mbNumberGetAccessor.GetILGenerator();
            // For an instance property, argument zero is the instance. Load the
            // instance, then load the private field and return, leaving the
            // field value on the stack.
            numberGetIL.Emit(OpCodes.Ldarg_0);
            numberGetIL.Emit(OpCodes.Ldfld, fieldBuilder);
            numberGetIL.Emit(OpCodes.Ret);
            // Last, map the "set" accessor methods to the
            // PropertyBuilder. The property is now only readable.
            pbNumber.SetGetMethod(mbNumberGetAccessor);
            
            // Define the "set" accessor method for Number, which has no return
            // type and takes one argument of type int (Int32).
            MethodBuilder mbNumberSetAccessor = typeBuilder.DefineMethod(
                $"set_{propertyName}",
                getSetAttr,
                null,
                new[] { parameterType });

            ILGenerator numberSetIL = mbNumberSetAccessor.GetILGenerator();
            // Load the instance and then the numeric argument, then store the
            // argument in the field.
            numberSetIL.Emit(OpCodes.Ldarg_0);
            numberSetIL.Emit(OpCodes.Ldarg_1);
            numberSetIL.Emit(OpCodes.Stfld, fieldBuilder);
            numberSetIL.Emit(OpCodes.Ret);

            // Last, map the "set" accessor methods to the
            // PropertyBuilder. The property is now read-writable.
            pbNumber.SetSetMethod(mbNumberSetAccessor);
        }
        
        PropertyBuilder pbMethod = typeBuilder.DefineProperty(
            "Method",
            PropertyAttributes.HasDefault,
            typeof(string),
            null);
        
        MethodBuilder mbGetMethod = typeBuilder.DefineMethod(
            "get_Method",
            getSetAttr,
            typeof(string),
            Type.EmptyTypes
        );

        ILGenerator getMethodIL = mbGetMethod.GetILGenerator();
        getMethodIL.Emit(OpCodes.Ldstr, alias.ToString());
        getMethodIL.Emit(OpCodes.Ret);
        
        pbMethod.SetGetMethod(mbGetMethod);

        ConstructorInfo ciTypeIndicator = typeof(TypeIndicatorAttribute).GetConstructor(new[] { typeof(ComparingOptions) });
        CustomAttributeBuilder cabTypeIndicator = new CustomAttributeBuilder(ciTypeIndicator, new object[] { ComparingOptions.Default });
        pbMethod.SetCustomAttribute(cabTypeIndicator);

        // Define a default constructor without arguments
        ConstructorBuilder ctor0 = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            Type.EmptyTypes);
        ILGenerator ctor0Il = ctor0.GetILGenerator();
        ctor0Il.Emit(OpCodes.Ret);

        // Define a constructor that takes an integer argument and
        // stores it in the private field.
        ConstructorBuilder ctor1 = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            parameterTypes);

        ILGenerator ctor1Il = ctor1.GetILGenerator();
        // For a constructor, argument zero is a reference to the new
        // instance. Push it on the stack before calling the base
        // class constructor. Specify the default constructor of the
        // base class (System.Object) by passing an empty array of
        // types (Type.EmptyTypes) to GetConstructor.
        ctor1Il.Emit(OpCodes.Ldarg_0);
        ctor1Il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
        // Push the instance on the stack before pushing the argument
        // that is to be assigned to the private field m_number.
        for (i = 0; i < parameters.Length; ++i)
        {
            ctor1Il.Emit(OpCodes.Ldarg_0);
            if (i == 0)
                ctor1Il.Emit(OpCodes.Ldarg_1);
            else if (i == 1)
                ctor1Il.Emit(OpCodes.Ldarg_2);
            else if (i == 2)
                ctor1Il.Emit(OpCodes.Ldarg_3);
            else
                ctor1Il.Emit(OpCodes.Ldarg_S, i + 1);
            ctor1Il.Emit(OpCodes.Stfld, fieldBuilders[i]);
        }
        ctor1Il.Emit(OpCodes.Ret);
        
        // Finish the type.
        return typeBuilder.CreateType();
    }
}