import os
import sys
import shutil
import clr

script_dir = os.path.dirname(os.path.abspath(__file__))

# Cari folder game XDLands
if len(sys.argv) > 1 and os.path.exists(sys.argv[1]):
    dll_dir = sys.argv[1]
elif os.path.exists(os.path.join(script_dir, "XDLands.exe")):
    dll_dir = script_dir
else:
    local_app = os.environ.get("LOCALAPPDATA", "")
    dll_dir = os.path.join(local_app, r"XDLands\PC\game")

managed_dir = os.path.join(dll_dir, r"XDLands_Data\Managed")

if not os.path.exists(managed_dir):
    print(f"[ERROR] Direktori Managed game tidak ditemukan di: {managed_dir}")
    print("Penggunaan: python patch_mod.py <path_ke_folder_game>")
    sys.exit(1)

# Cari Mono.Cecil.dll
cecil_path = os.path.join(script_dir, "Mono.Cecil.dll")
if not os.path.exists(cecil_path):
    cecil_path = os.path.join(dll_dir, "Mono.Cecil.dll")
if not os.path.exists(cecil_path):
    print(f"[ERROR] Mono.Cecil.dll tidak ditemukan!")
    sys.exit(1)

clr.AddReference(cecil_path)

from Mono.Cecil import (
    AssemblyDefinition, MethodAttributes, ParameterDefinition,
    ParameterAttributes, TypeReference, MethodReference,
    DefaultAssemblyResolver, ReaderParameters, MethodDefinition,
    FieldDefinition, FieldAttributes, GenericInstanceType, FieldReference
)
from Mono.Cecil.Cil import Instruction, ExceptionHandler, ExceptionHandlerType, OpCodes, VariableDefinition

import System

def patch_all():
    asm_path = os.path.join(managed_dir, "Assembly-CSharp.dll")
    backup_path = os.path.join(managed_dir, "Assembly-CSharp.dll.original")
    
    if not os.path.exists(backup_path):
        if os.path.exists(asm_path):
            shutil.copyfile(asm_path, backup_path)
            print(f"[BACKUP] Membuat backup Assembly-CSharp.dll.original di: {backup_path}")
        else:
            print(f"[ERROR] File {asm_path} tidak ditemukan!")
            sys.exit(1)

    with open(backup_path, "rb") as f:
        asm_bytes = f.read()
    ms = System.IO.MemoryStream(asm_bytes)

    res = DefaultAssemblyResolver()
    res.AddSearchDirectory(managed_dir)
    res.AddSearchDirectory(script_dir)
    res.AddSearchDirectory(dll_dir)
    r_params = ReaderParameters()
    r_params.AssemblyResolver = res
    c_asm = AssemblyDefinition.ReadAssembly(ms, r_params)
    module = c_asm.MainModule

    mscorlib_ref = next(ar for ar in module.AssemblyReferences if ar.Name == "mscorlib")
    core_ref = next(ar for ar in module.AssemblyReferences if ar.Name == "UnityEngine.CoreModule")

    # 1. Craft limit (15 -> 999)
    t_craft = next((t for t in module.GetTypes() if t.Name == "CraftSlotContainer"), None)
    if t_craft:
        m_craft = next((m for m in t_craft.Methods if m.Name == "CalcMaxQuantity"), None)
        if m_craft:
            craft_mods = 0
            for inst in m_craft.Body.Instructions:
                if inst.OpCode == OpCodes.Ldc_I4_S and inst.Operand == 15:
                    inst.OpCode = OpCodes.Ldc_I4
                    inst.Operand = System.Int32(999)
                    craft_mods += 1
            print(f"1. Craft limit di-patch (15 -> 999): {craft_mods} instructions.")

    # 2. Anti-Stagger (DisableStagger field & Blow/KnockBack bypass)
    dp = next((t for t in module.GetTypes() if t.Name == "DamagedProcesser"), None)
    if dp:
        f_disable = FieldDefinition("DisableStagger", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Boolean)
        dp.Fields.Add(f_disable)
        
        m_blow = next((m for m in dp.Methods if m.Name == "Blow"), None)
        m_kb = next((m for m in dp.Methods if m.Name == "KnockBack"), None)
        for m in [m_blow, m_kb]:
            if m and m.HasBody and len(m.Body.Instructions) > 0:
                first = m.Body.Instructions[0]
                il = m.Body.GetILProcessor()
                ins_ld = il.Create(OpCodes.Ldsfld, f_disable)
                ins_br = il.Create(OpCodes.Brfalse_S, first)
                ins_ret = il.Create(OpCodes.Ret)
                il.InsertBefore(first, ins_ld)
                il.InsertBefore(first, ins_br)
                il.InsertBefore(first, ins_ret)
        print("2. Anti-Stagger di-patch ke DamagedProcesser.")

    # 3. Lock Default Run Animation (Cap MoveSpeed at 500)
    t_lmu = next((t for t in module.GetTypes() if t.Name == "LocalMotionUpdater"), None)
    if t_lmu:
        f_lock = FieldDefinition("LockDefaultRunAnim", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Boolean)
        t_lmu.Fields.Add(f_lock)

        mscorlib_asm = AssemblyDefinition.ReadAssembly(os.path.join(managed_dir, "mscorlib.dll"))
        math_t = next(t for t in mscorlib_asm.MainModule.Types if t.Name == "Math")
        min_m = next(m for m in math_t.Methods if m.Name == "Min" and len(m.Parameters) == 2 and m.Parameters[0].ParameterType.Name == "Int32")
        ref_min = module.ImportReference(min_m)
        
        m_gsca = next((m for m in t_lmu.Methods if m.Name == "GetStateConditionArguments"), None)
        if m_gsca and m_gsca.HasBody:
            il = m_gsca.Body.GetILProcessor()
            conv_inst = None
            next_inst = None
            for idx, inst in enumerate(m_gsca.Body.Instructions):
                if inst.OpCode == OpCodes.Conv_I4 and inst.Next and inst.Next.OpCode == OpCodes.Newobj and "Nullable" in str(inst.Next.Operand):
                    conv_inst = inst
                    next_inst = inst.Next
                    break
            if conv_inst and next_inst:
                ins_ldsfld = il.Create(OpCodes.Ldsfld, f_lock)
                ins_brfalse = il.Create(OpCodes.Brfalse_S, next_inst)
                ins_ldc = il.Create(OpCodes.Ldc_I4, System.Int32(500))
                ins_call = il.Create(OpCodes.Call, ref_min)
                
                il.InsertAfter(conv_inst, ins_call)
                il.InsertAfter(conv_inst, ins_ldc)
                il.InsertAfter(conv_inst, ins_brfalse)
                il.InsertAfter(conv_inst, ins_ldsfld)
                print("3. LockDefaultRunAnim di-patch ke LocalMotionUpdater.")

    # 4. UsingAction.Update OnAttackPacket Hook
    ua = next((t for t in module.GetTypes() if t.FullName == "Durango.Logic.Combat.UsingAction"), None)
    uba = next((t for t in module.GetTypes() if t.FullName == "Messages.UseBattleAction"), None)
    if ua and uba:
        action_gen_type = TypeReference("System", "Action`1", module, mscorlib_ref)
        action_uba_type = GenericInstanceType(action_gen_type)
        action_uba_type.GenericArguments.Add(uba)
        action_ua = FieldDefinition("OnAttackPacket", FieldAttributes.Public | FieldAttributes.Static, action_uba_type)
        ua.Fields.Add(action_ua)
        
        m_update = next((m for m in ua.Methods if m.Name == "Update"), None)
        if m_update and m_update.HasBody:
            il = m_update.Body.GetILProcessor()
            
            existing_op = None
            for t in module.GetTypes():
                for m in t.Methods:
                    if m.HasBody:
                        for i in m.Body.Instructions:
                            s = str(i.Operand)
                            if ("Action`1" in s or "Action<" in s) and "Invoke" in s:
                                existing_op = i.Operand
                                break
                        if existing_op: break
                if existing_op: break
            
            action_invoke = MethodReference("Invoke", module.TypeSystem.Void, action_uba_type)
            action_invoke.HasThis = True
            for p in existing_op.Parameters:
                action_invoke.Parameters.Add(ParameterDefinition(p.Name, p.Attributes, p.ParameterType))
            
            call_send_idx = -1
            for idx, inst in enumerate(m_update.Body.Instructions):
                if inst.OpCode == OpCodes.Callvirt and "Connection::Send<Messages.UseBattleAction>" in str(inst.Operand):
                    call_send_idx = idx
                    break
            if call_send_idx != -1:
                v_uba = m_update.Body.Variables[17]
                pop_inst = m_update.Body.Instructions[call_send_idx + 1]
                target_next = pop_inst.Next
                
                ins_ldsfld = il.Create(OpCodes.Ldsfld, action_ua)
                ins_brfalse = il.Create(OpCodes.Brfalse_S, target_next)
                ins_ldsfld2 = il.Create(OpCodes.Ldsfld, action_ua)
                ins_ldmsg = il.Create(OpCodes.Ldloc, v_uba)
                ins_call = il.Create(OpCodes.Callvirt, action_invoke)
                
                il.InsertBefore(target_next, ins_ldsfld)
                il.InsertBefore(target_next, ins_brfalse)
                il.InsertBefore(target_next, ins_ldsfld2)
                il.InsertBefore(target_next, ins_ldmsg)
                il.InsertBefore(target_next, ins_call)
                print("4. OnAttackPacket hook di-patch ke UsingAction.Update.")

        # 4b. Moving Attack: Keep StopMove so melee range is normal, bypass ONLY MoveLock=true
        f_movelock = FieldDefinition("DisableAttackMoveLock", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Boolean)
        ua.Fields.Add(f_movelock)

        call_stop_idx = -1
        for idx, inst in enumerate(m_update.Body.Instructions):
            if inst.OpCode == OpCodes.Callvirt and "PlayerController::StopMove" in str(inst.Operand):
                call_stop_idx = idx
                break

        if call_stop_idx != -1:
            inst_isys_instance = m_update.Body.Instructions[call_stop_idx + 1]
            inst_target = m_update.Body.Instructions[call_stop_idx + 4]

            ins_ld_ml = il.Create(OpCodes.Ldsfld, f_movelock)
            ins_br_ml = il.Create(OpCodes.Brtrue, inst_target)

            il.InsertBefore(inst_isys_instance, ins_ld_ml)
            il.InsertBefore(inst_isys_instance, ins_br_ml)
            print("4b. DisableAttackMoveLock di-patch untuk Moving Attack.")

    # 4c. Dino Noclip cleanly patched into LocalUnitPushArea.ProcessUnitPush
    upa = next((t for t in module.GetTypes() if t.FullName == "LocalUnitPushArea"), None)
    if upa:
        f_noclip = FieldDefinition("EnableDinoNoclip", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Boolean)
        upa.Fields.Add(f_noclip)

        m_pup = next((m for m in upa.Methods if m.Name == "ProcessUnitPush"), None)
        if m_pup and m_pup.HasBody:
            il_pup = m_pup.Body.GetILProcessor()
            inst_6 = m_pup.Body.Instructions[6]
            
            ins_ld_nc = il_pup.Create(OpCodes.Ldsfld, f_noclip)
            ins_br_nc = il_pup.Create(OpCodes.Brfalse_S, inst_6)
            ins_ret_false = il_pup.Create(OpCodes.Ldc_I4_0)
            ins_ret = il_pup.Create(OpCodes.Ret)
            
            il_pup.InsertBefore(inst_6, ins_ld_nc)
            il_pup.InsertBefore(inst_6, ins_br_nc)
            il_pup.InsertBefore(inst_6, ins_ret_false)
            il_pup.InsertBefore(inst_6, ins_ret)
            print("4c. Dino Noclip di-patch ke LocalUnitPushArea.ProcessUnitPush.")

    # 4d. Dual Speed Multiplier (Player & Dino isolated, zero overlap)
    pc_type = next((t for t in module.GetTypes() if t.Name == "PlayerController"), None)
    if pc_type:
        f_player_speed = FieldDefinition("ModPlayerSpeed", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Single)
        f_mount_speed = FieldDefinition("ModMountSpeed", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Single)
        pc_type.Fields.Add(f_player_speed)
        pc_type.Fields.Add(f_mount_speed)

        m_get_player = next(m for m in pc_type.Methods if m.Name == "get_Player")
        pb_type = next(t for t in module.GetTypes() if t.Name == "PlayerBehavior")
        m_get_is_riding = next(m for m in pb_type.Methods if m.Name == "get_IsRiding")

        m_get_mult = MethodDefinition("GetActiveMoveSpeedMultiplier", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Single)
        il_mult = m_get_mult.Body.GetILProcessor()

        ins_not_riding = il_mult.Create(OpCodes.Ldsfld, f_player_speed)
        ins_ret_one = il_mult.Create(OpCodes.Ldc_R4, System.Single(1.0))

        il_mult.Append(il_mult.Create(OpCodes.Call, m_get_player))
        il_mult.Append(il_mult.Create(OpCodes.Brfalse_S, ins_not_riding))
        il_mult.Append(il_mult.Create(OpCodes.Call, m_get_player))
        il_mult.Append(il_mult.Create(OpCodes.Callvirt, m_get_is_riding))
        il_mult.Append(il_mult.Create(OpCodes.Brfalse_S, ins_not_riding))

        # Riding -> ModMountSpeed
        il_mult.Append(il_mult.Create(OpCodes.Ldsfld, f_mount_speed))
        il_mult.Append(il_mult.Create(OpCodes.Ldc_R4, System.Single(0.01)))
        il_mult.Append(il_mult.Create(OpCodes.Ble_Un_S, ins_ret_one))
        il_mult.Append(il_mult.Create(OpCodes.Ldsfld, f_mount_speed))
        il_mult.Append(il_mult.Create(OpCodes.Ret))

        # Not Riding -> ModPlayerSpeed
        il_mult.Append(ins_not_riding)
        il_mult.Append(il_mult.Create(OpCodes.Ldc_R4, System.Single(0.01)))
        il_mult.Append(il_mult.Create(OpCodes.Ble_Un_S, ins_ret_one))
        il_mult.Append(il_mult.Create(OpCodes.Ldsfld, f_player_speed))
        il_mult.Append(il_mult.Create(OpCodes.Ret))

        # Fallback 1.0f
        il_mult.Append(ins_ret_one)
        il_mult.Append(il_mult.Create(OpCodes.Ret))

        pc_type.Methods.Add(m_get_mult)

        m_get_speed = next(m for m in pc_type.Methods if m.Name == "get_MoveSpeed")
        if m_get_speed and m_get_speed.HasBody:
            il_speed = m_get_speed.Body.GetILProcessor()
            for idx in range(len(m_get_speed.Body.Instructions) - 1, -1, -1):
                inst = m_get_speed.Body.Instructions[idx]
                if inst.OpCode == OpCodes.Call and "CheatMoveSpeedMultiply" in str(inst.Operand):
                    prev_inst = m_get_speed.Body.Instructions[idx - 1]
                    prev_inst.OpCode = OpCodes.Call
                    prev_inst.Operand = m_get_mult
                    il_speed.Remove(inst)
                    break
        print("4d. Dual Speed Multiplier di-patch ke PlayerController.get_MoveSpeed.")

    # 5. Inject LoadAutoGather from MarkHelper.dll into XDLands.Mark
    helper_asm_path = os.path.join(script_dir, "MarkHelper.dll")
    if not os.path.exists(helper_asm_path):
        helper_asm_path = os.path.join(dll_dir, "scratch", "MarkHelper.dll")
    if not os.path.exists(helper_asm_path):
        helper_asm_path = os.path.join(dll_dir, "MarkHelper.dll")
    
    helper_asm = AssemblyDefinition.ReadAssembly(helper_asm_path)
    helper_type = next(t for t in helper_asm.MainModule.Types if t.Name == "MarkHelper")
    helper_method = next(m for m in helper_type.Methods if m.Name == "LoadAutoGather")

    mark_type = next((t for t in module.GetTypes() if t.FullName == "XDLands.Mark"), None)
    if mark_type:
        ensure_m = next((m for m in mark_type.Methods if m.Name == "Ensure"), None)
        if ensure_m:
            new_load_m = MethodDefinition("LoadAutoGather", helper_method.Attributes, module.TypeSystem.Void)
            go_type_ref = module.ImportReference(helper_method.Parameters[0].ParameterType)
            new_load_m.Parameters.Add(ParameterDefinition("go", getattr(ParameterAttributes, "None"), go_type_ref))

            for v in helper_method.Body.Variables:
                new_load_m.Body.Variables.Add(VariableDefinition(module.ImportReference(v.VariableType)))

            inst_map = {}
            il = new_load_m.Body.GetILProcessor()
            for inst in helper_method.Body.Instructions:
                op = inst.Operand
                if isinstance(op, TypeReference):
                    op = module.ImportReference(op)
                elif isinstance(op, MethodReference):
                    op = module.ImportReference(op)
                elif isinstance(op, FieldReference):
                    op = module.ImportReference(op)
                
                new_inst = Instruction.Create(inst.OpCode) if op is None else il.Create(inst.OpCode, op)
                inst_map[inst] = new_inst
                new_load_m.Body.Instructions.Add(new_inst)

            for old_inst, new_inst in inst_map.items():
                if old_inst.Operand in inst_map:
                    new_inst.Operand = inst_map[old_inst.Operand]
                elif isinstance(old_inst.Operand, list) or hasattr(old_inst.Operand, '__iter__') and not isinstance(old_inst.Operand, (str, bytes)):
                    try:
                        new_inst.Operand = [inst_map[t] for t in old_inst.Operand]
                    except: pass

            for eh in helper_method.Body.ExceptionHandlers:
                new_eh = ExceptionHandler(eh.HandlerType)
                if eh.CatchType:
                    new_eh.CatchType = module.ImportReference(eh.CatchType)
                new_eh.TryStart = inst_map[eh.TryStart]
                new_eh.TryEnd = inst_map[eh.TryEnd]
                new_eh.HandlerStart = inst_map[eh.HandlerStart]
                new_eh.HandlerEnd = inst_map[eh.HandlerEnd]
                new_load_m.Body.ExceptionHandlers.Add(new_eh)

            mark_type.Methods.Add(new_load_m)
            print("5. LoadAutoGather di-import ke XDLands.Mark.")

            target_inst = None
            for inst in ensure_m.Body.Instructions:
                if inst.OpCode == OpCodes.Call and "ShaderCheck::Begin" in str(inst.Operand):
                    target_inst = inst
                    break
            
            if target_inst:
                il_ensure = ensure_m.Body.GetILProcessor()
                il_ensure.InsertBefore(target_inst, il_ensure.Create(OpCodes.Ldloc_0))
                il_ensure.InsertBefore(target_inst, il_ensure.Create(OpCodes.Call, new_load_m))
                print("5. Ensure() di-patch untuk memanggil LoadAutoGather saat boot!")
            else:
                print("Warning: ShaderCheck::Begin tidak ditemukan di Ensure()!")

    # 6. AutoCounter hook into CombatSystem.OnDamaged & CombatSystem.OnAttackAlert
    counter_dll_path = os.path.join(managed_dir, "XDLands.AutoCounter.dll")
    if not os.path.exists(counter_dll_path):
        counter_dll_path = os.path.join(dll_dir, "scratch", "AutoCounter.dll")
    if not os.path.exists(counter_dll_path):
        counter_dll_path = os.path.join(script_dir, "XDLands.AutoCounter.dll")

    if os.path.exists(counter_dll_path):
        counter_asm = AssemblyDefinition.ReadAssembly(counter_dll_path, r_params)
        auto_counter_t = next((t for t in counter_asm.MainModule.Types if t.FullName == "XDLands.CombatMods.AutoCounter"), None)
        if auto_counter_t:
            on_damaged_m = next((m for m in auto_counter_t.Methods if m.Name == "OnDamaged"), None)
            on_alert_m = next((m for m in auto_counter_t.Methods if m.Name == "OnAttackAlert"), None)
            
            ref_on_damaged = module.ImportReference(on_damaged_m)
            ref_on_alert = module.ImportReference(on_alert_m)

            t_damaged = next((t for t in module.GetTypes() if t.FullName == "Messages.Damaged"), None)
            f_attacker_id = next(f for f in t_damaged.Fields if f.Name == "AttackerId")
            f_victim_id = next(f for f in t_damaged.Fields if f.Name == "VictimId")

            t_alert = next((t for t in module.GetTypes() if t.FullName == "Messages.AttackAlerted"), None)
            f_entity_id = next(f for f in t_alert.Fields if f.Name == "EntityId")

            t_cs = next((t for t in module.GetTypes() if t.Name == "CombatSystem"), None)
            if t_cs:
                m_cs_damaged = next((m for m in t_cs.Methods if m.Name == "OnDamaged" and len(m.Parameters) == 2), None)
                if m_cs_damaged and m_cs_damaged.HasBody:
                    il_d = m_cs_damaged.Body.GetILProcessor()
                    ret_d = [i for i in m_cs_damaged.Body.Instructions if i.OpCode == OpCodes.Ret][-1]
                    p_msg = m_cs_damaged.Parameters[0]
                    il_d.InsertBefore(ret_d, il_d.Create(OpCodes.Ldarga_S, p_msg))
                    il_d.InsertBefore(ret_d, il_d.Create(OpCodes.Ldfld, f_attacker_id))
                    il_d.InsertBefore(ret_d, il_d.Create(OpCodes.Ldarga_S, p_msg))
                    il_d.InsertBefore(ret_d, il_d.Create(OpCodes.Ldfld, f_victim_id))
                    il_d.InsertBefore(ret_d, il_d.Create(OpCodes.Call, ref_on_damaged))
                    print("6. AutoCounter OnDamaged di-patch ke CombatSystem.")

                m_cs_alert = next((m for m in t_cs.Methods if m.Name == "OnAttackAlert" and len(m.Parameters) == 2), None)
                if m_cs_alert and m_cs_alert.HasBody:
                    il_a = m_cs_alert.Body.GetILProcessor()
                    ret_a = [i for i in m_cs_alert.Body.Instructions if i.OpCode == OpCodes.Ret][-1]
                    p_alert = m_cs_alert.Parameters[0]
                    il_a.InsertBefore(ret_a, il_a.Create(OpCodes.Ldarga_S, p_alert))
                    il_a.InsertBefore(ret_a, il_a.Create(OpCodes.Ldfld, f_entity_id))
                    il_a.InsertBefore(ret_a, il_a.Create(OpCodes.Call, ref_on_alert))
                    print("6. AutoCounter OnAttackAlert di-patch ke CombatSystem.")

    c_asm.Write(asm_path)
    print(f"[BERHASIL] Assembly-CSharp.dll berhasil di-patch dan disimpan di: {asm_path}")

    # Copy AutoGatherMod.dll to Managed
    src_mod = os.path.join(script_dir, "AutoGatherMod.dll")
    if not os.path.exists(src_mod):
        src_mod = os.path.join(dll_dir, "AutoGatherMod.dll")
    if os.path.exists(src_mod):
        dst_mod = os.path.join(managed_dir, "AutoGatherMod.dll")
        try:
            shutil.copyfile(src_mod, dst_mod)
            print(f"[BERHASIL] AutoGatherMod.dll disalin ke: {dst_mod}")
        except Exception as e:
            print(f"Catatan: AutoGatherMod.dll tidak dapat ditimpa: {e}")

    # Copy AutoGatherSettings.ini to game root
    src_ini = os.path.join(script_dir, "AutoGatherSettings.ini")
    dst_ini = os.path.join(dll_dir, "AutoGatherSettings.ini")
    if os.path.exists(src_ini) and not os.path.exists(dst_ini):
        try:
            shutil.copyfile(src_ini, dst_ini)
            print(f"[BERHASIL] AutoGatherSettings.ini disalin ke: {dst_ini}")
        except Exception as e:
            print(f"Catatan: AutoGatherSettings.ini gagal disalin: {e}")

if __name__ == "__main__":
    patch_all()
