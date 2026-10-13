import os
import shutil
import clr

dll_dir = os.path.dirname(os.path.abspath(__file__))
managed_dir = os.path.join(dll_dir, r"XDLands_Data\Managed")
mark_dir = os.path.join(dll_dir, "XDLandsMark")

clr.AddReference(os.path.join(dll_dir, "Mono.Cecil.dll"))
from Mono.Cecil import (
    AssemblyDefinition, MethodAttributes, ParameterDefinition,
    ParameterAttributes, TypeReference, MethodReference,
    DefaultAssemblyResolver, ReaderParameters, MethodDefinition
)
from Mono.Cecil.Cil import Instruction, ExceptionHandler, ExceptionHandlerType, OpCodes, VariableDefinition

original_path = os.path.join(mark_dir, "XDLandsMark.dll.original")
target_path = os.path.join(mark_dir, "XDLandsMark.dll")

# Check if target_path is fresh (unpatched from game update)
import System.IO
if os.path.exists(target_path):
    with open(target_path, "rb") as f:
        bytes_data = f.read()
    ms = System.IO.MemoryStream(bytes_data)
    check_asm = AssemblyDefinition.ReadAssembly(ms)
    check_mark = next((t for t in check_asm.MainModule.Types if t.Name == "Mark"), None)
    is_patched = check_mark and any(m.Name == "LoadAutoGather" for m in check_mark.Methods)
    try:
        check_asm.Dispose()
    except:
        pass
    ms.Dispose()
    if not is_patched:
        shutil.copyfile(target_path, original_path)
        print("Fresh game update detected! Updated original backup at:", original_path)
    elif not os.path.exists(original_path):
        shutil.copyfile(target_path, original_path)
        print("Backup created at:", original_path)
elif not os.path.exists(original_path):
    shutil.copyfile(target_path, original_path)
    print("Backup created at:", original_path)

resolver = DefaultAssemblyResolver()
resolver.AddSearchDirectory(managed_dir)
resolver.AddSearchDirectory(mark_dir)
rp = ReaderParameters()
rp.AssemblyResolver = resolver

asm = AssemblyDefinition.ReadAssembly(original_path, rp)
mod = asm.MainModule
core = AssemblyDefinition.ReadAssembly(os.path.join(managed_dir, "UnityEngine.CoreModule.dll"), rp)
mscorlib = AssemblyDefinition.ReadAssembly(os.path.join(managed_dir, "mscorlib.dll"), rp)

# 1. Resolve methods
path_type = next(t for t in mscorlib.MainModule.Types if t.Name == "Path")
get_full_path = next(m for m in path_type.Methods if m.Name == "GetFullPath" and len(m.Parameters) == 1 and m.Parameters[0].ParameterType.Name == "String")

asm_type = next(t for t in mscorlib.MainModule.Types if t.Name == "Assembly")
load_from = next(m for m in asm_type.Methods if m.Name == "LoadFrom" and len(m.Parameters) == 1 and m.Parameters[0].ParameterType.Name == "String")
get_type_m = next(m for m in asm_type.Methods if m.Name == "GetType" and len(m.Parameters) == 1 and m.Parameters[0].ParameterType.Name == "String")

go_type = next(t for t in core.MainModule.Types if t.Name == "GameObject")
add_comp = next(m for m in go_type.Methods if m.Name == "AddComponent" and len(m.Parameters) == 1 and m.Parameters[0].ParameterType.Name == "Type")

debug_type = next(t for t in core.MainModule.Types if t.Name == "Debug")
log_m = next(m for m in debug_type.Methods if m.Name == "Log" and len(m.Parameters) == 1)
log_warn_m = next(m for m in debug_type.Methods if m.Name == "LogWarning" and len(m.Parameters) == 1)

ex_type = next(t for t in mscorlib.MainModule.Types if t.Name == "Exception")
obj_type = next(t for t in mscorlib.MainModule.Types if t.Name == "Object")
to_string_m = next(m for m in obj_type.Methods if m.Name == "ToString" and len(m.Parameters) == 0)

str_type = next(t for t in mscorlib.MainModule.Types if t.Name == "String")
concat_m = next(m for m in str_type.Methods if m.Name == "Concat" and len(m.Parameters) == 2 and m.Parameters[0].ParameterType.Name == "String")
type_type = next(t for t in mscorlib.MainModule.Types if t.Name == "Type")

# 2. Import into target module
ref_get_full_path = mod.ImportReference(get_full_path)
ref_load_from = mod.ImportReference(load_from)
ref_get_type_m = mod.ImportReference(get_type_m)
ref_add_comp = mod.ImportReference(add_comp)
ref_log = mod.ImportReference(log_m)
ref_log_warn = mod.ImportReference(log_warn_m)
ref_ex_type = mod.ImportReference(ex_type)
ref_to_string = mod.ImportReference(to_string_m)
ref_concat = mod.ImportReference(concat_m)
ref_go_type = mod.ImportReference(go_type)
ref_asm_type = mod.ImportReference(asm_type)
ref_type_type = mod.ImportReference(type_type)

# 3. Create LoadAutoGather(GameObject go) method
load_method = MethodDefinition("LoadAutoGather", MethodAttributes.Public | MethodAttributes.Static, mod.TypeSystem.Void)
none_param_attr = getattr(ParameterAttributes, "None")
load_method.Parameters.Add(ParameterDefinition("go", none_param_attr, ref_go_type))
load_method.Body.Variables.Add(VariableDefinition(ref_asm_type))
load_method.Body.Variables.Add(VariableDefinition(ref_type_type))
load_method.Body.Variables.Add(VariableDefinition(ref_ex_type))

il = load_method.Body.GetILProcessor()
ret_inst = il.Create(OpCodes.Ret)
skip_add = il.Create(OpCodes.Nop)
leave_try = il.Create(OpCodes.Leave_S, ret_inst)
catch_start = il.Create(OpCodes.Stloc_2)
leave_catch = il.Create(OpCodes.Leave_S, ret_inst)

# Try block:
try_start = il.Create(OpCodes.Ldstr, "AutoGatherMod.dll")
il.Append(try_start)
il.Append(il.Create(OpCodes.Call, ref_get_full_path))
il.Append(il.Create(OpCodes.Call, ref_load_from))
il.Append(il.Create(OpCodes.Stloc_0))

il.Append(il.Create(OpCodes.Ldloc_0))
il.Append(il.Create(OpCodes.Ldstr, "AutoGather.AutoGatherController"))
il.Append(il.Create(OpCodes.Callvirt, ref_get_type_m))
il.Append(il.Create(OpCodes.Stloc_1))

il.Append(il.Create(OpCodes.Ldloc_1))
il.Append(il.Create(OpCodes.Brfalse_S, skip_add))

il.Append(il.Create(OpCodes.Ldarg_0))
il.Append(il.Create(OpCodes.Ldloc_1))
il.Append(il.Create(OpCodes.Callvirt, ref_add_comp))
il.Append(il.Create(OpCodes.Pop))

il.Append(il.Create(OpCodes.Ldstr, "[AutoGather] Loaded and attached to XDLandsMark!"))
il.Append(il.Create(OpCodes.Call, ref_log))

il.Append(skip_add)
il.Append(leave_try)

# Catch block:
il.Append(catch_start)
il.Append(il.Create(OpCodes.Ldstr, "[AutoGather] Load failed: "))
il.Append(il.Create(OpCodes.Ldloc_2))
il.Append(il.Create(OpCodes.Callvirt, ref_to_string))
il.Append(il.Create(OpCodes.Call, ref_concat))
il.Append(il.Create(OpCodes.Call, ref_log_warn))
il.Append(leave_catch)

il.Append(ret_inst)

eh = ExceptionHandler(ExceptionHandlerType.Catch)
eh.CatchType = ref_ex_type
eh.TryStart = try_start
eh.TryEnd = catch_start
eh.HandlerStart = catch_start
eh.HandlerEnd = ret_inst
load_method.Body.ExceptionHandlers.Add(eh)

mark = next(t for t in mod.Types if t.Name == "Mark")
mark.Methods.Add(load_method)
print("LoadAutoGather added to XDLands.Mark!")

# 4. Patch Ensure()
ensure = next(m for m in mark.Methods if m.Name == "Ensure")
il_ens = ensure.Body.GetILProcessor()
ret_ens = ensure.Body.Instructions[len(ensure.Body.Instructions) - 1]

first_injected = il_ens.Create(OpCodes.Ldloc_0)
call_injected = il_ens.Create(OpCodes.Call, load_method)

il_ens.InsertBefore(ret_ens, first_injected)
il_ens.InsertBefore(ret_ens, call_injected)

# Redirect branches that target ret_ens to first_injected
for inst in ensure.Body.Instructions:
    if inst.Operand == ret_ens:
        inst.Operand = first_injected

# Adjust exception handler end if it was ret_ens
for handler in ensure.Body.ExceptionHandlers:
    if handler.HandlerEnd == ret_ens:
        handler.HandlerEnd = first_injected

print("Ensure() patched successfully!")

asm.Write(target_path)
print("SUCCESS: XDLandsMark.dll patched and saved at", target_path)

# 5. Patch Craft Limit in Assembly-CSharp.dll (15 -> 999)
def patch_craft_limit():
    asm_path = os.path.join(managed_dir, "Assembly-CSharp.dll")
    backup_path = os.path.join(managed_dir, "Assembly-CSharp.dll.original")
    if not os.path.exists(backup_path):
        shutil.copyfile(asm_path, backup_path)
    
    res = DefaultAssemblyResolver()
    res.AddSearchDirectory(managed_dir)
    r_params = ReaderParameters()
    r_params.AssemblyResolver = res
    c_asm = AssemblyDefinition.ReadAssembly(asm_path, r_params)
    t = next((t for t in c_asm.MainModule.Types if t.Name == "CraftSlotContainer"), None)
    if t is None:
        c_asm.Dispose()
        return
    m = next((m for m in t.Methods if m.Name == "CalcMaxQuantity"), None)
    if m is None:
        c_asm.Dispose()
        return
    
    modified = 0
    import System
    for inst in m.Body.Instructions:
        if inst.OpCode == OpCodes.Ldc_I4_S and inst.Operand == 15:
            inst.OpCode = OpCodes.Ldc_I4
            inst.Operand = System.Int32(999)
            modified += 1
    
    if modified > 0:
        tmp = os.path.join(managed_dir, "Assembly-CSharp.dll.tmp")
        c_asm.Write(tmp)
        c_asm.Dispose()
        shutil.move(tmp, asm_path)
        print(f"Patched CraftSlotContainer.CalcMaxQuantity limit from 15 to 999 ({modified} instructions)!")
    else:
        c_asm.Dispose()
        print("CraftSlotContainer.CalcMaxQuantity already patched (limit >= 999).")

try:
    patch_craft_limit()
except Exception as ex:
    print("Warning: Failed to patch craft limit in Assembly-CSharp.dll:", ex)
