/*
 * Copyright 2019 faddenSoft
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace SourceGen.Res {
    /// <summary>
    /// This is a bridge between the XAML definitions and the C# code that uses the strings.
    /// FindResource() throws an exception if the resource isn't found, so typos and missing
    /// resources will cause the app to fail the first time any string is referenced.
    /// </summary>
    public static class Strings {
        /// <summary>
        /// Name of the embedded copy of Strings.xaml, which remains the single source of
        /// truth for the string table.  Set explicitly in the .csproj.
        /// </summary>
        private const string RESOURCE_NAME = "SourceGen.Res.Strings.xaml";

        private static Dictionary<string, string> sStringMap;

        /// <summary>
        /// Looks up a string by resource key.  Throws if the key is missing, so typos and
        /// omitted resources surface the first time a string is referenced (this matches
        /// the behaviour of the WPF FindResource() call this replaced).
        /// </summary>
        private static string Map(string key) {
            Dictionary<string, string> map = sStringMap;
            if (map == null) {
                map = LoadStringMap();
                sStringMap = map;
            }
            if (!map.TryGetValue(key, out string value)) {
                throw new KeyNotFoundException("String resource not found: " + key);
            }
            return value;
        }

        private static Dictionary<string, string> LoadStringMap() {
            Dictionary<string, string> map = new Dictionary<string, string>();
            using (Stream stream = typeof(Strings).Assembly.GetManifestResourceStream(RESOURCE_NAME)) {
                if (stream == null) {
                    throw new InvalidOperationException("Embedded resource not found: " + RESOURCE_NAME);
                }
                XDocument doc = XDocument.Load(stream);
                XNamespace xns = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
                foreach (XElement elem in doc.Descendants()) {
                    if (elem.Name.LocalName != "String") {
                        continue;
                    }
                    XAttribute keyAttr = elem.Attribute(xns + "Key");
                    if (keyAttr != null) {
                        map[keyAttr.Value] = elem.Value;
                    }
                }
            }
            return map;
        }
        public static string ABBREV_ADDRESS => Map("str_AbbrevAddress");
        public static string ABBREV_CONSTANT => Map("str_AbbrevConstant");
        public static string ABBREV_STACK_RELATIVE => Map("str_AbbrevStackRelative");
        public static string ANALYZER_TAG_MULTI_CHK => Map("str_AnalyzerTagMultiChk");
        public static string ANALYZER_TAG_JMP_TABLE_CAPTION => Map("str_AnalyzerTagJmpTableCaption");
        public static string ANALYZER_TAG_JMP_TABLE_FMT => Map("str_AnalyzerTagJmpTableFmt");
        public static string ASM_LATEST_VERSION => Map("str_AsmLatestVersion");
        public static string ASM_MATCH_FAILURE => Map("str_AsmMatchFailure");
        public static string ASM_MATCH_SUCCESS => Map("str_AsmMatchSuccess");
        public static string ASM_MISMATCH_CAPTION => Map("str_AsmMismatchCaption");
        public static string ASM_MISMATCH_DATA_FMT => Map("str_AsmMismatchDataFmt");
        public static string ASM_MISMATCH_LENGTH_FMT => Map("str_AsmMismatchLengthFmt");
        public static string ASM_OUTPUT_NOT_FOUND => Map("str_AsmOutputNotFound");
        public static string AUTO_SAVE_OFF => Map("str_AutoSaveOff");
        public static string AUTO_SAVE_1_MIN => Map("str_AutoSave1Min");
        public static string AUTO_SAVE_5_MIN => Map("str_AutoSave5Min");
        public static string CONFIRMATION_NEEDED => Map("str_ConfirmationNeeded");
        public static string DATA_BANK_AUTO_FMT => Map("str_DataBankAutoFmt");
        public static string DATA_BANK_USER_FMT => Map("str_DataBankUserFmt");
        public static string DATA_BANK_K => Map("str_DataBankK");
        public static string DEFAULT_HEADER_COMMENT_FMT => Map("str_DefaultHeaderCommentFmt");
        public static string CLIPFORMAT_ALL_COLUMNS => Map("str_ClipformatAllColumns");
        public static string CLIPFORMAT_ASSEMBLER_SOURCE => Map("str_ClipformatAssemblerSource");
        public static string CLIPFORMAT_DISASSEMBLY => Map("str_ClipformatDisassembly");
        public static string EQU_ADDRESS => Map("str_EquAddress");
        public static string EQU_CONSTANT => Map("str_EquConstant");
        public static string EQU_STACK_RELATIVE => Map("str_EquStackRelative");
        public static string ERR_BAD_ADDRESS_REGION_FMT => Map("str_ErrBadAddressRegionFmt");
        public static string ERR_BAD_DEF_SYMBOL_DIR => Map("str_ErrBadDefSymbolDir");
        public static string ERR_BAD_FD_FMT => Map("str_ErrBadFdFmt");
        public static string ERR_BAD_FD_FORMAT => Map("str_ErrBadFdFormat");
        public static string ERR_BAD_FILE_LENGTH => Map("str_ErrBadFileLength");
        public static string ERR_BAD_IDENT => Map("str_ErrBadIdent");
        public static string ERR_BAD_LOCAL_VARIABLE_FMT => Map("str_ErrBadLocalVariableFmt");
        public static string ERR_BAD_LV_TABLE_FMT => Map("str_ErrBadLvTableFmt");
        public static string ERR_BAD_RANGE => Map("str_ErrBadRange");
        public static string ERR_BAD_SYMBOL_LABEL => Map("str_ErrBadSymbolLabel");
        public static string ERR_BAD_SYMBOL_ST => Map("str_ErrBadSymbolSt");
        public static string ERR_BAD_SYMREF_PART => Map("str_ErrBadSymrefPart");
        public static string ERR_BAD_ANALYZER_TAG => Map("str_ErrBadAnalyzerTag");
        public static string ERR_BAD_VISUALIZATION_FMT => Map("str_ErrBadVisualizationFmt");
        public static string ERR_BAD_VISUALIZATION_SET_FMT => Map("str_ErrBadVisualizationSetFmt");
        public static string ERR_DIR_CREATE_FAILED_FMT => Map("str_ErrDirCreateFailedFmt");
        public static string ERR_DUPLICATE_LABEL_FMT => Map("str_ErrDuplicateLabelFmt");
        public static string ERR_FILE_COPY_FAILED_FMT => Map("str_ErrFileCopyFailedFmt");
        public static string ERR_FILE_EXISTS_NOT_DIR_FMT => Map("str_ErrFileExistsNotDirFmt");
        public static string ERR_FILE_GENERIC_CAPTION => Map("str_ErrFileGenericCaption");
        public static string ERR_FILE_NOT_FOUND_FMT => Map("str_ErrFileNotFoundFmt");
        public static string ERR_FILE_READ_FAILED_FMT => Map("str_ErrFileReadFailedFmt");
        public static string ERR_FILE_READ_ONLY_FMT => Map("str_ErrFileReadOnlyFmt");
        public static string ERR_INVALID_ADDRESS_MASK => Map("str_ErrInvalidAddressMask");
        public static string ERR_INVALID_CMP_ADDR_OVERLAP => Map("str_ErrInvalidCmpAddrOverlap");
        public static string ERR_INVALID_CMP_EXTRA_BITS => Map("str_ErrInvalidCmpExtraBits");
        public static string ERR_INVALID_COMPARE_MASK => Map("str_ErrInvalidCompareMask");
        public static string ERR_INVALID_COMPARE_VALUE => Map("str_ErrInvalidCompareValue");
        public static string ERR_INVALID_INT_VALUE => Map("str_ErrInvalidIntValue");
        public static string ERR_INVALID_KEY_VALUE => Map("str_ErrInvalidKeyValue");
        public static string ERR_INVALID_MULTI_MASK => Map("str_ErrInvalidMultiMask");
        public static string ERR_INVALID_WIDTH => Map("str_ErrInvalidWidth");
        public static string ERR_INVALID_SYSDEF => Map("str_ErrInvalidSysdef");
        public static string ERR_LOAD_CONFIG_FILE => Map("str_ErrLoadConfigFile");
        public static string ERR_NOT_PROJECT_FILE => Map("str_ErrNotProjectFile");
        public static string ERR_PROJECT_FILE_CORRUPT => Map("str_ErrProjectFileCorrupt");
        public static string ERR_PROJECT_LOAD_FAIL => Map("str_ErrProjectLoadFail");
        public static string ERR_PROJECT_SAVE_FAIL => Map("str_ErrProjectSaveFail");
        public static string ERR_TOO_LARGE_FOR_PREVIEW => Map("str_ErrTooLargeForPreview");
        public static string ERR_VALUE_INCOMPATIBLE_WITH_MASK => Map("str_ErrValueIncompatibleWithMask");
        public static string EXPORTING_HTML => Map("str_ExportingHtml");
        public static string EXPORTING_HTML_AND_IMAGES => Map("str_ExportingHtmlAndImages");
        public static string EXTERNAL_FILE_BAD_DIR_FMT => Map("str_ExternalFileBadDirFmt");
        public static string EXTERNAL_FILE_BAD_DIR_CAPTION => Map("str_ExternalFileBadDirCaption");
        public static string FILE_FILTER_ALL => Map("str_FileFilterAll");
        public static string FILE_FILTER_CS => Map("str_FileFilterCs");
        public static string FILE_FILTER_CSV => Map("str_FileFilterCsv");
        public static string FILE_FILTER_DIS65 => Map("str_FileFilterDis65");
        public static string FILE_FILTER_GIF => Map("str_FileFilterGif");
        public static string FILE_FILTER_HTML => Map("str_FileFilterHtml");
        public static string FILE_FILTER_SGEC => Map("str_FileFilterSgec");
        public static string FILE_FILTER_SYM65 => Map("str_FileFilterSym65");
        public static string FILE_FILTER_TEXT => Map("str_FileFilterText");
        public static string FILE_INFO_FMT => Map("str_FileInfoFmt");
        public static string FIND_ALL_CAPTION => Map("str_FindAllCaption");
        public static string FIND_ALL_NO_MATCH => Map("str_FindAllNoMatch");
        public static string FIND_REACHED_START => Map("str_FindReachedStart");
        public static string FIND_REACHED_START_CAPTION => Map("str_FindReachedStartCaption");
        public static string FONT_DESCRIPTOR_FMT => Map("str_FontDescriptorFmt");
        public static string GENERATED_FOR_VERSION_FMT => Map("str_GeneratedForVersion");
        public static string HIDE_COL => Map("str_HideCol");
        public static string INFO_AUTO_FORMAT => Map("str_InfoAutoFormat");
        public static string INFO_CUSTOM_FORMAT => Map("str_InfoCustomFormat");
        public static string INFO_DEFAULT_FORMAT => Map("str_InfoDefaultFormat");
        public static string INFO_LABEL_DESCR_FMT => Map("str_InfoLabelDescrFmt");
        public static string INFO_LINE_SUM_NON_FMT => Map("str_InfoLineSumNonFmt");
        public static string INFO_LINE_SUM_PLURAL_FMT => Map("str_InfoLineSumPluralFmt");
        public static string INFO_LINE_SUM_SINGULAR_FMT => Map("str_InfoLineSumSingularFmt");
        public static string INFO_MULTI_LINE_SUM_FMT => Map("str_InfoMultiLineSumFmt");
        public static string INFO_MULTI_LINE_START_FMT => Map("str_InfoMultiLineStartFmt");
        public static string INFO_MULTI_LINE_END_FMT => Map("str_InfoMultiLineEndFmt");
        public static string INFO_MULTI_LINE_LEN_FMT => Map("str_InfoMultiLineLenFmt");
        public static string INITIAL_EXTENSION_SCRIPTS => Map("str_InitialExtensionScripts");
        public static string INITIAL_PARAMETERS => Map("str_InitialParameters");
        public static string INITIAL_SYMBOL_FILES => Map("str_InitialSymbolFiles");
        public static string INVALID_ADDRESS => Map("str_InvalidAddress");
        public static string INVALID_FORMAT_WORD_SEL_CAPTION => Map("str_InvalidFormatWordSelCaption");
        public static string INVALID_FORMAT_WORD_SEL_NON1 => Map("str_InvalidFormatWordSelNon1");
        public static string INVALID_FORMAT_WORD_SEL_UNEVEN_FMT => Map("str_InvalidFormatWordSelUnevenFmt");
        public static string LOAD_ADDRESS => Map("str_LoadAddress");
        public static string LOCAL_VARIABLE_TABLE_CLEAR => Map("str_LocalVariableTableClear");
        public static string LOCAL_VARIABLE_TABLE_EMPTY => Map("str_LocalVariableTableEmpty");
        public static string MSG_BANK_OVERRUN => Map("str_MsgBankOverrun");
        public static string MSG_BANK_OVERRUN_DETAIL_FMT => Map("str_MsgBankOverrunDetailFmt");
        public static string MSG_FORMAT_DESCRIPTOR_IGNORED => Map("str_MsgFormatDescriptorIgnored");
        public static string MSG_HIDDEN_LABEL => Map("str_MsgHiddenLabel");
        public static string MSG_HIDDEN_LOCAL_VARIABLE_TABLE => Map("str_MsgHiddenLocalVariableTable");
        public static string MSG_HIDDEN_VISUALIZATION => Map("str_MsgHiddenVisualization");
        public static string MSG_INVALID_DESCRIPTOR => Map("str_MsgInvalidDescriptor");
        public static string MSG_INVALID_OFFSET_OR_LENGTH => Map("str_MsgInvalidOffsetOrLength");
        public static string MSG_LABEL_IGNORED => Map("str_MsgLabelIgnored");
        public static string MSG_LOCAL_VARIABLE_TABLE_IGNORED => Map("str_MsgLocalVariableTableIgnored");
        public static string MSG_NON_ADDR_LABEL_REF => Map("str_MsgNonAddrLabelRef");
        public static string MSG_UNRESOLVED_WEAK_REF => Map("str_MsgUnresolvedWeakRef");
        public static string MSG_VISUALIZATION_IGNORED => Map("str_MsgVisualizationIgnored");
        public static string NO_FILES_AVAILABLE => Map("str_NoFilesAvailable");
        public static string NO_EXPORTED_SYMBOLS_FOUND => Map("str_NoExportedSymbolsFound");
        public static string OMF_SEG_COMMENT_FMT => Map("str_OmfSegCommentFmt");
        public static string OMF_SEG_HDR_COMMENT_FMT => Map("str_OmfSegHdrCommentFmt");
        public static string OMF_SEG_NOTE_FMT => Map("str_OmfSegNoteFmt");
        public static string OMF_SELECT_FILE => Map("str_OmfSelectFile");
        public static string OPEN_DATA_DOESNT_EXIST => Map("str_OpenDataDoesntExist");
        public static string OPEN_DATA_EMPTY => Map("str_OpenDataEmpty");
        public static string OPEN_DATA_FAIL_CAPTION => Map("str_OpenDataFailCaption");
        public static string OPEN_DATA_FAIL_MESSAGE => Map("str_OpenDataFailMessage");
        public static string OPEN_DATA_PARTIAL_READ => Map("str_OpenDataPartialRead");
        public static string OPEN_DATA_LOAD_FAILED_FMT => Map("str_OpenDataLoadFailedFmt");
        public static string OPEN_DATA_TOO_LARGE_FMT => Map("str_OpenDataTooLargeFmt");
        public static string OPEN_DATA_TOO_SMALL_FMT => Map("str_OpenDataTooSmallFmt");
        public static string OPEN_DATA_WRONG_CRC_FMT => Map("str_OpenDataWrongCrcFmt");
        public static string OPEN_DATA_WRONG_LENGTH_FMT => Map("str_OpenDataWrongLengthFmt");
        public static string OPERATION_FAILED => Map("str_OperationFailed");
        public static string OPERATION_SUCCEEDED => Map("str_OperationSucceeded");
        public static string PARENTHETICAL_NONE => Map("str_ParentheticalNone");
        public static string PLUGIN_DIR_FAIL_FMT => Map("str_PluginDirFailFmt");
        public static string PLUGIN_DIR_FAIL_CAPTION => Map("str_PluginDirFailCaption");
        public static string PROGRESS_ASSEMBLING => Map("str_ProgressAssembling");
        public static string PROGRESS_GENERATING_FMT => Map("str_ProgressGeneratingFmt");
        public static string PROJECT_FIELD_COMMENT => Map("str_ProjectFieldComment");
        public static string PROJECT_FIELD_DBR_VALUE => Map("str_ProjectFieldDbrValue");
        public static string PROJECT_FIELD_LONG_COMMENT => Map("str_ProjectFieldLongComment");
        public static string PROJECT_FIELD_LV_TABLE => Map("str_ProjectFieldLvTable");
        public static string PROJECT_FIELD_MISC_FLAGS => Map("str_ProjectFieldMiscFlags");
        public static string PROJECT_FIELD_NOTE => Map("str_ProjectFieldNote");
        public static string PROJECT_FIELD_OPERAND_FORMAT => Map("str_ProjectFieldOperandFormat");
        public static string PROJECT_FIELD_RELOC_DATA => Map("str_ProjectFieldRelocData");
        public static string PROJECT_FIELD_STATUS_FLAGS => Map("str_ProjectFieldStatusFlags");
        public static string PROJECT_FIELD_ANALYZER_TAG => Map("str_ProjectFieldAnalyzerTag");
        public static string PROJECT_FIELD_USER_LABEL => Map("str_ProjectFieldUserLabel");
        public static string PROJECT_FROM_NEWER_APP => Map("str_ProjectFromNewerApp");
        //public static string RECENT_PROJECT_LINK_FMT =
        //    (string)Application.Current.FindResource("str_RecentProjectLinkFmt");
        public static string RUNTIME_DIR_NOT_FOUND => Map("str_RuntimeDirNotFound");
        public static string RUNTIME_DIR_NOT_FOUND_CAPTION => Map("str_RuntimeDirNotFoundCaption");
        public static string SAVE_BEFORE_ASM => Map("str_SaveBeforeAsm");
        public static string SAVE_BEFORE_ASM_CAPTION => Map("str_SaveBeforeAsmCaption");
        public static string SCAN_LOW_ASCII => Map("str_ScanLowAscii");
        public static string SCAN_LOW_HIGH_ASCII => Map("str_ScanLowHighAscii");
        public static string SCAN_C64_PETSCII => Map("str_ScanC64Petscii");
        public static string SCAN_C64_SCREEN_CODE => Map("str_ScanC64ScreenCode");
        public static string SETUP_SYSTEM_SUMMARY_FMT => Map("str_SetupSystemSummaryFmt");
        public static string SHOW_COL => Map("str_ShowCol");
        public static string STATUS_BYTE_COUNT_FMT => Map("str_StatusByteCountFmt");
        public static string STATUS_READY => Map("str_StatusReady");
        public static string STR_VFY_DCI_MIXED_DATA => Map("str_StrVfyDciMixedData");
        public static string STR_VFY_DCI_NOT_TERMINATED => Map("str_StrVfyDciNotTerminated");
        public static string STR_VFY_DCI_SHORT => Map("str_StrVfyDciShort");
        public static string STR_VFY_L1_LENGTH_MISMATCH => Map("str_StrVfyL1LengthMismatch");
        public static string STR_VFY_L2_LENGTH_MISMATCH => Map("str_StrVfyL2LengthMismatch");
        public static string STR_VFY_MISSING_NULL_TERM => Map("str_StrVfyMissingNullTerm");
        public static string STR_VFY_NULL_INSIDE_NULL_TERM => Map("str_StrVfyNullInsideNullTerm");
        public static string SYMBOL_IMPORT_CAPTION => Map("str_SymbolImportCaption");
        public static string SYMBOL_IMPORT_GOOD_FMT => Map("str_SymbolImportGoodFmt");
        public static string SYMBOL_IMPORT_NONE => Map("str_SymbolImportNone");
        public static string TIPS_LOADING => Map("str_TipsLoading");
        public static string TIPS_NOT_AVAILABLE => Map("str_TipsNotAvailable");
        public static string TITLE_BASE => Map("str_TitleBase");
        public static string TITLE_MODIFIED => Map("str_TitleModified");
        public static string TITLE_NEW_PROJECT => Map("str_TitleNewProject");
        public static string TITLE_READ_ONLY => Map("str_TitleReadOnly");
        public static string UNSET => Map("str_Unset");
        public static string VIS_SET_MULTIPLE_FMT => Map("str_VisSetMultipleFmt");
        public static string VIS_SET_SINGLE_FMT => Map("str_VisSetSingleFmt");
    }
}
