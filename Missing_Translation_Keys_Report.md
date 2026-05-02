# Translation Key Analysis Report

Great news! The missing keys identified in the previous check have been successfully added to `Languages/English/Keyed/AutomatedWorkAssignment_Keys.xml`. The `AWA_PriorityFieldLabel` formatting issue has also been resolved.

However, there are still several **Hardcoded Strings** in the C# code that are not using the translation system. These strings will appear in English regardless of the selected language.

## 1. Unused Key in XML

The following key exists in your XML but is not being used in the code (the code currently uses a hardcoded string):

| Key Name | XML Value | Current Code Use | Location |
| :--- | :--- | :--- | :--- |
| `AWA_ConfigureExpertModeTooltip` | *Click to open the advanced rule editor...* | `"Configure skill-based priority rules (Expert Mode)."` | `AutomatedWorkAssignmentMod.cs` (Line 175) |

## 2. Hardcoded Strings (Missing Keys)

The following UI elements are defined with string literals in the code. To support translation, you should create keys for them and update the code to use `.Translate()`.

### `AutomatedWorkAssignmentMod.cs`

| UI Element | Current Hardcoded String | Suggested Key Name |
| :--- | :--- | :--- |
| Label | `"Simple Mode"` | `AWA_AssignmentMode_Simple` |
| Tooltip | `"Uses only the Count/Priority sliders below..."` | `AWA_AssignmentMode_Simple_Tooltip` |
| Label | `"Expert Mode"` | `AWA_AssignmentMode_Expert` |
| Tooltip | `"Uses ONLY skill-based rules from Expert Mode..."` | `AWA_AssignmentMode_Expert_Tooltip` |
| Label | `"Hybrid Mode"` | `AWA_AssignmentMode_Hybrid` |
| Tooltip | `"Expert rules override when they match..."` | `AWA_AssignmentMode_Hybrid_Tooltip` |
| Checkbox | `"Force Emergency Priorities (Doctor/Firefighter = P1)"` | `AWA_ForceEmergencyPriorities` |
| Tooltip | `"When enabled, Doctor and Firefighter are always forced..."` | `AWA_ForceEmergencyPriorities_Tooltip` |
| Checkbox | `"Prioritize Passion in Expert Mode"` | `AWA_PrioritizePassion` |
| Tooltip | `"When enabled, pawns are sorted by passion FIRST..."` | `AWA_PrioritizePassion_Tooltip` |
| Button | `"Mode: %"` / `"Mode: #"` | `AWA_ModeToggle_Percentage` / `AWA_ModeToggle_Count` |
| Formatting | `"Passion: {0}x"` | `AWA_PassionWeightLabel` (Use `{0}`) |
| Tooltip | `"How much passion affects assignment priority..."` | `AWA_PassionWeight_Tooltip` |
| Checkbox | `"Backup: {0}"` / `"OFF"` | `AWA_BackupPriorityLabel` / `AWA_Off` |
| Tooltip | `"Priority for colonists NOT in top selection..."` | `AWA_BackupPriority_Tooltip` |

### `Dialog_ExpertModeSettings.cs`

| UI Element | Current Hardcoded String | Suggested Key Name |
| :--- | :--- | :--- |
| Button | `"Exclude Pawns"` | `AWA_ExcludePawnsButton` |
| Tooltip | `"Select specific pawns to exclude from this job type."` | `AWA_ExcludePawns_Tooltip` |
| Tooltip | `"Adds a new skill-based priority rule for this work type."` | `AWA_AddRule_Tooltip` |
| Warning | `"⚠ WARNING: {0} has no skill associated..."` | `AWA_NoSkillWarning` |
| Info Text | `"Expert Mode uses the Count/Percentage settings..."` | `AWA_ExpertMode_Info` |
| Tooltip | `"Select {0} to edit rules."` | `AWA_WorkTypeSelect_Tooltip` |
| Tooltip | `"{0} has no associated skill..."` | `AWA_WorkTypeNoSkill_Tooltip` |

### `Dialog_ManageExclusions.cs`
*None found (Good job!)*

### `HarmonyPatches.cs`
*None found (Good job!)*
