﻿using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Core.Helpers {
	/// <summary>
	///     Provides methods to inject a <see cref="TypeDef" /> into another module.
	/// </summary>
	public static class InjectHelper {
		/// <summary>
		///     Clones the specified origin TypeDef.
		/// </summary>
		/// <param name="origin">The origin TypeDef.</param>
		/// <returns>The cloned TypeDef.</returns>
		static TypeDefUser Clone(TypeDef origin) {
			var ret = new TypeDefUser(origin.Namespace, origin.Name);
			ret.Attributes = origin.Attributes;

			if (origin.ClassLayout != null)
				ret.ClassLayout = new ClassLayoutUser(origin.ClassLayout.PackingSize, origin.ClassSize);

			foreach (GenericParam genericParam in origin.GenericParameters)
				ret.GenericParameters.Add(new GenericParamUser(genericParam.Number, genericParam.Flags, "-"));

			return ret;
		}

		/// <summary>
		///     Clones the specified origin MethodDef.
		/// </summary>
		/// <param name="origin">The origin MethodDef.</param>
		/// <returns>The cloned MethodDef.</returns>
		static MethodDefUser Clone(MethodDef origin) {
			var ret = new MethodDefUser(origin.Name, null, origin.ImplAttributes, origin.Attributes);

			foreach (GenericParam genericParam in origin.GenericParameters)
				ret.GenericParameters.Add(new GenericParamUser(genericParam.Number, genericParam.Flags, "-"));

			return ret;
		}

		/// <summary>
		///     Clones the specified origin FieldDef.
		/// </summary>
		/// <param name="origin">The origin FieldDef.</param>
		/// <returns>The cloned FieldDef.</returns>
		static FieldDefUser Clone(FieldDef origin) {
			var ret = new FieldDefUser(origin.Name, null, origin.Attributes);
			return ret;
		}

		/// <summary>
		///     Populates the context mappings.
		/// </summary>
		/// <param name="typeDef">The origin TypeDef.</param>
		/// <param name="ctx">The injection context.</param>
		/// <returns>The new TypeDef.</returns>
		static TypeDef PopulateContext(TypeDef typeDef, InjectContext ctx) {
			TypeDef ret;
			if (!ctx.MemberMap.TryGetValue(typeDef, out var existing)) {
				ret = Clone(typeDef);
				ctx.MemberMap[typeDef] = ret;
			}
			else
				ret = (TypeDef)existing;

			foreach (TypeDef nestedType in typeDef.NestedTypes)
				ret.NestedTypes.Add(PopulateContext(nestedType, ctx));

			foreach (MethodDef method in typeDef.Methods)
				ret.Methods.Add((MethodDef)(ctx.MemberMap[method] = Clone(method)));

			foreach (FieldDef field in typeDef.Fields)
				ret.Fields.Add((FieldDef)(ctx.MemberMap[field] = Clone(field)));

			return ret;
		}

		/// <summary>
		///     Copies the information from the origin type to injected type.
		/// </summary>
		/// <param name="typeDef">The origin TypeDef.</param>
		/// <param name="ctx">The injection context.</param>
		static void CopyTypeDef(TypeDef typeDef, InjectContext ctx) {
			var newTypeDef = (TypeDef)ctx.MemberMap[typeDef];

			newTypeDef.BaseType = ctx.Importer.Import(typeDef.BaseType);

			foreach (InterfaceImpl iface in typeDef.Interfaces)
				newTypeDef.Interfaces.Add(new InterfaceImplUser(ctx.Importer.Import(iface.Interface)));
		}

		/// <summary>
		///     Copies the information from the origin method to injected method.
		/// </summary>
		/// <param name="methodDef">The origin MethodDef.</param>
		/// <param name="ctx">The injection context.</param>
		static void CopyMethodDef(MethodDef methodDef, InjectContext ctx) {
			var newMethodDef = (MethodDef)ctx.MemberMap[methodDef];

			newMethodDef.Signature = ctx.Importer.Import(methodDef.Signature);
			newMethodDef.Parameters.UpdateParameterTypes();

			if (methodDef.ImplMap != null)
				newMethodDef.ImplMap = new ImplMapUser(new ModuleRefUser(ctx.TargetModule, methodDef.ImplMap.Module.Name), methodDef.ImplMap.Name, methodDef.ImplMap.Attributes);

			foreach (CustomAttribute ca in methodDef.CustomAttributes)
				newMethodDef.CustomAttributes.Add(new CustomAttribute((ICustomAttributeType)ctx.Importer.Import(ca.Constructor)));

			if (methodDef.HasBody) {
				newMethodDef.Body = new CilBody(methodDef.Body.InitLocals, new List<Instruction>(), new List<ExceptionHandler>(), new List<Local>());
				newMethodDef.Body.MaxStack = methodDef.Body.MaxStack;

				var bodyMap = new Dictionary<object, object>();

				foreach (Local local in methodDef.Body.Variables) {
					var newLocal = new Local(ctx.Importer.Import(local.Type));
					newMethodDef.Body.Variables.Add(newLocal);
					newLocal.Name = local.Name;

					bodyMap[local] = newLocal;
				}

				foreach (Instruction instr in methodDef.Body.Instructions) {
					var newInstr = new Instruction(instr.OpCode, instr.Operand);
					newInstr.SequencePoint = instr.SequencePoint;

					if (newInstr.Operand is IType)
						newInstr.Operand = ctx.Importer.Import((IType)newInstr.Operand);

					else if (newInstr.Operand is IMethod)
						newInstr.Operand = ctx.Importer.Import((IMethod)newInstr.Operand);

					else if (newInstr.Operand is IField)
						newInstr.Operand = ctx.Importer.Import((IField)newInstr.Operand);

					newMethodDef.Body.Instructions.Add(newInstr);
					bodyMap[instr] = newInstr;
				}

				foreach (Instruction instr in newMethodDef.Body.Instructions) {
					if (instr.Operand != null && bodyMap.ContainsKey(instr.Operand))
						instr.Operand = bodyMap[instr.Operand];

					else if (instr.Operand is Instruction[])
						instr.Operand = ((Instruction[])instr.Operand).Select(target => (Instruction)bodyMap[target]).ToArray();
				}

				foreach (ExceptionHandler eh in methodDef.Body.ExceptionHandlers)
					newMethodDef.Body.ExceptionHandlers.Add(new ExceptionHandler(eh.HandlerType) {
						CatchType = eh.CatchType == null ? null : ctx.Importer.Import(eh.CatchType),
						TryStart = (Instruction)bodyMap[eh.TryStart],
						TryEnd = (Instruction)bodyMap[eh.TryEnd],
						HandlerStart = (Instruction)bodyMap[eh.HandlerStart],
						HandlerEnd = (Instruction)bodyMap[eh.HandlerEnd],
						FilterStart = eh.FilterStart == null ? null : (Instruction)bodyMap[eh.FilterStart]
					});

				newMethodDef.Body.SimplifyMacros(newMethodDef.Parameters);
			}
		}

		/// <summary>
		///     Copies the information from the origin field to injected field.
		/// </summary>
		/// <param name="fieldDef">The origin FieldDef.</param>
		/// <param name="ctx">The injection context.</param>
		static void CopyFieldDef(FieldDef fieldDef, InjectContext ctx) {
			var newFieldDef = (FieldDef)ctx.MemberMap[fieldDef];

			newFieldDef.Signature = ctx.Importer.Import(fieldDef.Signature);
		}

		/// <summary>
		///     Copies the information to the injected definitions.
		/// </summary>
		/// <param name="typeDef">The origin TypeDef.</param>
		/// <param name="ctx">The injection context.</param>
		/// <param name="copySelf">if set to <c>true</c>, copy information of <paramref name="typeDef" />.</param>
		static void Copy(TypeDef typeDef, InjectContext ctx, bool copySelf) {
			if (copySelf)
				CopyTypeDef(typeDef, ctx);

			foreach (TypeDef nestedType in typeDef.NestedTypes)
				Copy(nestedType, ctx, true);

			foreach (MethodDef method in typeDef.Methods)
				CopyMethodDef(method, ctx);

			foreach (FieldDef field in typeDef.Fields)
				CopyFieldDef(field, ctx);
		}

		/// <summary>
		///     Injects the specified TypeDef to another module.
		/// </summary>
		/// <param name="typeDef">The source TypeDef.</param>
		/// <param name="target">The target module.</param>
		/// <returns>The injected TypeDef.</returns>
		public static TypeDef Inject(TypeDef typeDef, ModuleDef target) {
			var ctx = new InjectContext(typeDef.Module, target);
			PopulateContext(typeDef, ctx);
			Copy(typeDef, ctx, true);
			return (TypeDef)ctx.MemberMap[typeDef];
		}

		/// <summary>
		///     Injects the specified MethodDef to another module.
		/// </summary>
		/// <param name="methodDef">The source MethodDef.</param>
		/// <param name="target">The target module.</param>
		/// <returns>The injected MethodDef.</returns>
		public static MethodDef Inject(MethodDef methodDef, ModuleDef target) {
			var ctx = new InjectContext(methodDef.Module, target);
			ctx.MemberMap[methodDef] = Clone(methodDef);
			CopyMethodDef(methodDef, ctx);
			return (MethodDef)ctx.MemberMap[methodDef];
		}

		/// <summary>
		///     Injects the members of specified TypeDef to another module.
		/// </summary>
		/// <param name="typeDef">The source TypeDef.</param>
		/// <param name="newType">The new type.</param>
		/// <param name="target">The target module.</param>
		/// <returns>Injected members.</returns>
		public static IEnumerable<IDnlibDef> Inject(TypeDef typeDef, TypeDef newType, ModuleDef target) {
			var ctx = new InjectContext(typeDef.Module, target);
			ctx.MemberMap[typeDef] = newType;
			PopulateContext(typeDef, ctx);
			Copy(typeDef, ctx, false);
			return ctx.MemberMap.Values.Except(new[] { newType }).OfType<IDnlibDef>();
		}

		/// <summary>
		///     Context of the injection process.
		/// </summary>
		class InjectContext : ImportMapper {
			/// <summary>
			///     The mapping of origin definitions to injected definitions.
			/// </summary>
			public readonly Dictionary<IMemberRef, IMemberRef> MemberMap = new Dictionary<IMemberRef, IMemberRef>();

			/// <summary>
			///     The module which source type originated from.
			/// </summary>
			public readonly ModuleDef OriginModule;

			/// <summary>
			///     The module which source type is being injected to.
			/// </summary>
			public readonly ModuleDef TargetModule;

			/// <summary>
			///     The importer.
			/// </summary>
			readonly Importer importer;

			/// <summary>
			///     Initializes a new instance of the <see cref="InjectContext" /> class.
			/// </summary>
			/// <param name="module">The origin module.</param>
			/// <param name="target">The target module.</param>
			public InjectContext(ModuleDef module, ModuleDef target) {
				OriginModule = module;
				TargetModule = target;
				importer = new Importer(target, ImporterOptions.TryToUseTypeDefs, new GenericParamContext(), this);
			}

			/// <summary>
			///     Gets the importer.
			/// </summary>
			/// <value>The importer.</value>
			public Importer Importer {
				get { return importer; }
			}

			/// <inheritdoc />
			public override ITypeDefOrRef Map(ITypeDefOrRef source) {
				if (MemberMap.TryGetValue(source, out var result))
					return (ITypeDefOrRef)result;

				//HACK: for netcore
				//System.Enviroment and System.AppDomain is in System.Runtime.Extensions/mscorlib/netstandard
				//System.Runtime.InteropServices.Marshal is in System.Runtime.InteropServices/mscorlib/netstandard
				if (source.IsTypeRef && OriginModule.CorLibTypes.AssemblyRef != TargetModule.CorLibTypes.AssemblyRef &&
					source.DefinitionAssembly == OriginModule.CorLibTypes.AssemblyRef) {
					var sourceRef = (TypeRef)source;
					TypeRef typeRef = TryResolveType(sourceRef, TargetModule.CorLibTypes.AssemblyRef, false);
					if (typeRef == null) {
						if (source.DefinitionAssembly.Name != "mscorlib")
							typeRef = TryResolveType(sourceRef, new AssemblyRefUser(source.DefinitionAssembly), false);

						typeRef = typeRef ?? TryResolveType(sourceRef, new AssemblyRefUser("netstandard"), true) ??
							TryResolveType(sourceRef, new AssemblyRefUser("mscorlib"), true) ??
							TryResolveType(sourceRef, new AssemblyRefUser("System.Private.CoreLib"), false);
					}
					if (typeRef != null) {
						TargetModule.UpdateRowId(typeRef.ResolutionScope);
						TargetModule.UpdateRowId(typeRef);
						MemberMap[source] = typeRef;
						return typeRef;
					}
				}

				return null;
			}

			TypeRef TryResolveType(TypeRef sourceRef, AssemblyRef scope, bool checkExport) {
				var typeRef = Import2(sourceRef, scope);

				if (checkExport) {
					var scopeDef = TargetModule.Context.AssemblyResolver.Resolve(typeRef.DefinitionAssembly, TargetModule);
					if (scopeDef != null) {
						var sigComparer = new SigComparer(SigComparerOptions.DontCompareTypeScope);
						var exportType = scopeDef.Modules.SelectMany(m => m.ExportedTypes).Where(et => sigComparer.Equals(et, typeRef)).FirstOrDefault();
						if (exportType != null && exportType.Implementation.Name != "System.Private.CoreLib" && exportType.Resolve() != null) {
							return exportType.ToTypeRef();
						}
					}
				}

				if (typeRef.Resolve() != null)
					return typeRef;

				return null;
			}

			TypeRef Import2(TypeRef type, IResolutionScope scope) {
				if (type is null)
					return null;
				TypeRef result;

				var declaringType = type.DeclaringType;
				if (!(declaringType is null))
					result = new TypeRefUser(TargetModule, type.Namespace, type.Name, Import2(declaringType, scope));
				else
					result = new TypeRefUser(TargetModule, type.Namespace, type.Name, scope);

				return result;
			}

			/// <inheritdoc />
			public override IMethod Map(MethodDef source) {
				if (MemberMap.TryGetValue(source, out var result))
					return (MethodDef)result;
				return null;
			}

			/// <inheritdoc />
			public override IField Map(FieldDef source) {
				if (MemberMap.ContainsKey(source))
					return (FieldDef)MemberMap[source];
				return null;
			}
		}
	}
}
