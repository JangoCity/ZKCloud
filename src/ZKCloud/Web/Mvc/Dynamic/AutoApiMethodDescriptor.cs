﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace ZKCloud.Web.Mvc.Dynamic {
    public class AutoApiMethodDescriptor {
        public Type DeclaringType { get { return Method.DeclaringType; } }

        public MethodInfo Method { get; private set; }

        public AutoApiMethodDescriptor(MethodInfo method) {
            Method = method;
        }

        public void CreateDynamicMethod(TypeBuilder builder) {
            var methodParameters = Method.GetParameters();
            var methodBuilder = builder.DefineMethod(Method.Name,
                MethodAttributes.Public,
                Method.ReturnType,
                methodParameters.Select(e => e.ParameterType).ToArray());
            for (int i = 0; i < methodParameters.Length; i++) {
                methodBuilder.DefineParameter(i + 1, ParameterAttributes.HasDefault, methodParameters[i].Name);
            }
            var il = methodBuilder.GetILGenerator();
            var resolveMethod = typeof(BaseController)
                .GetMethod("Resolve", BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(DeclaringType);
            LocalBuilder serviceLocal = il.DeclareLocal(DeclaringType);
            il.Emit(OpCodes.Ldarg_0);
            il.EmitCall(OpCodes.Call, resolveMethod, null);
            il.Emit(OpCodes.Stloc, serviceLocal);
            il.Emit(OpCodes.Ldloc, serviceLocal);
            for (int i = 0; i < methodParameters.Length; i++) {
                il.Emit(OpCodes.Ldarg, i + 1);
            }
            il.EmitCall(OpCodes.Call, Method, null);
            il.Emit(OpCodes.Ret);
        }
    }
}
