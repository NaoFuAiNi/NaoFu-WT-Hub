/* nf_patcher.c - 字体替换流程 */

#define _CRT_SECURE_NO_WARNINGS
#include "nf_patcher.h"
#include "nf_types.h"
#include "nf_console.h"
#include "nf_fonts.h"
#include "nf_io.h"
#include "nf_bin.h"
#include "nf_subset.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

static const char* const ORIGINAL_BIN_PATH     = "font/source/fonts.vromfs.bin";
static const char* const SOURCE_BIN_PATH       = "font/source/src_font_bin/fonts.vromfs.bin";  /* 未修改的源 bin，仅读取不修改 */
static const char* const REPLACE_FILE_PATH    = "font/custom/MyFonts.ttf";
static const char* const BASE_SEARCH_PATH     = "font/source/fonts.vromfs.bin_u/";

#define PROJECT_PATH_MAX 256

s32 NF_runFontReplace(u32 font_choice, const char* project_dir, int auto_slim) {
    char search_file_path[256];
    char output_bin_path[PROJECT_PATH_MAX];
    char slim_font_path[PROJECT_PATH_MAX];
    const NF_FontChoice_t* selected_font = NULL;
    NF_byte_t* vromfs_data = NULL;
    NF_byte_t* search_data = NULL;
    NF_byte_t* replace_data_padded = NULL;
    NF_file_size_t vromfs_size, search_size, replace_size;
    u32 i;

    if (!project_dir || project_dir[0] == '\0') return -1;
    {
        int n;
        n = snprintf(output_bin_path, sizeof(output_bin_path), "%s/fonts.vromfs.bin", project_dir);
        if (n < 0 || (size_t)n >= sizeof(output_bin_path)) return -1;
        n = snprintf(slim_font_path, sizeof(slim_font_path), "%s/MyFonts_slim.ttf", project_dir);
        if (n < 0 || (size_t)n >= sizeof(slim_font_path)) return -1;
    }

    /* 若本项目目录里已有 fonts.vromfs.bin，则基于它继续修改（多次替换会累积）；否则从 font/source 读取原版 */
    const char* vromfs_input_path;
    if (NF_pathExists(output_bin_path))
        vromfs_input_path = output_bin_path;
    else
        vromfs_input_path = ORIGINAL_BIN_PATH;

    for (i = 0; i < g_num_font_choices; ++i) {
        if (g_font_choices[i].id == font_choice) {
            selected_font = &g_font_choices[i];
            break;
        }
    }
    if (selected_font == NULL) {
        nf_printf("\n[NaoFu] 错误：序号 %u 超出范围，请输入有效的序号。\n", font_choice);
        return -1;
    }

    {
        int n = snprintf(search_file_path, sizeof(search_file_path), "%s%s", BASE_SEARCH_PATH, selected_font->original_filename);
        if (n < 0 || (size_t)n >= sizeof(search_file_path)) {
            nf_printf("\n[NaoFu] 错误：路径过长。\n");
            return -1;
        }
    }
    nf_printf("\n[NaoFu] 您已选择替换: [%s]\n", selected_font->display_name);
    nf_printf("[NaoFu] 目标文件路径: %s\n", search_file_path);
    nf_printf("----------------------------------------------------\n");

    nf_printf("[NaoFu] 正在读取文件...\n");
    if (NF_readFileToBuffer(vromfs_input_path, &vromfs_data, &vromfs_size) != 0) {
        nf_printf("[NaoFu] 错误：无法读取 %s，请确认文件存在且未被占用。\n", vromfs_input_path);
        return -1;
    }
    if (vromfs_input_path == output_bin_path)  /* 同一指针表示本次从项目 bin 继续 */
        nf_printf("[NaoFu]  > 成功读取已修改的 %s, 大小: %ld 字节（在已有修改基础上继续）\n", output_bin_path, vromfs_size);
    else
        nf_printf("[NaoFu]  > 成功读取官方 fonts.vromfs.bin 文件, 大小: %ld 字节\n", vromfs_size);

    if (NF_readFileToBuffer(search_file_path, &search_data, &search_size) != 0) {
        nf_printf("[NaoFu] 错误：无法读取基准字体 %s。\n", search_file_path);
        free(vromfs_data);
        return -1;
    }
    nf_printf("[NaoFu]  > 成功读取搜索基准文件, 大小: %ld 字节\n", search_size);

    if (NF_readFileToBuffer(REPLACE_FILE_PATH, &replace_data_padded, &replace_size) != 0) {
        nf_printf("[NaoFu] 错误：无法读取自定义字体 %s，请先在界面中选择字体。\n", REPLACE_FILE_PATH);
        free(vromfs_data);
        free(search_data);
        return -1;
    }
    nf_printf("[NaoFu]  > 成功读取您的自定义字体, 大小: %ld 字节\n", replace_size);

    if (replace_size > search_size) {
        if (!auto_slim) {
            nf_printf("\n[NaoFu] 您的自定义字体 (%ld 字节) 大于原版 (%ld 字节)，无法直接替换。\n", replace_size, search_size);
            nf_printf("[NaoFu] 是否自动瘦身（按原版字符集子集化）后再替换？(y/n): ");
            {
                char ch;
                scanf(" %c", &ch);
                while (getchar() != '\n') { }
                if (ch != 'y' && ch != 'Y') {
                    nf_printf("[NaoFu] 已取消。请将字体瘦身后再试，或选择其他字体槽位。\n");
                    free(vromfs_data);
                    free(search_data);
                    free(replace_data_padded);
                    return -1;
                }
            }
        }
        nf_printf("[NaoFu] 正在调用瘦身工具（瘦身 + 西里尔 T/U/E 映射）...\n");
        if (NF_createDir(project_dir) != 0) {
            nf_printf("[NaoFu] 无法创建项目目录 %s。\n", project_dir);
            free(vromfs_data);
            free(search_data);
            free(replace_data_padded);
            return -1;
        }
        if (NF_runSubsetTool(search_file_path, REPLACE_FILE_PATH, slim_font_path) != 0) {
            nf_printf("[NaoFu] 瘦身失败。请确保 nf_subset_tool.exe 与主程序在同一目录。\n");
            free(vromfs_data);
            free(search_data);
            free(replace_data_padded);
            return -1;
        }
        nf_printf("[NaoFu] 瘦身完成，正在读取瘦身后的字体...\n");
        free(replace_data_padded);
        replace_data_padded = NULL;
        if (NF_readFileToBuffer(slim_font_path, &replace_data_padded, &replace_size) != 0) {
            nf_printf("[NaoFu] 读取瘦身字体失败。\n");
            free(vromfs_data);
            free(search_data);
            return -1;
        }
        nf_printf("[NaoFu]  > 瘦身后大小: %ld 字节\n", replace_size);
        if (replace_size > search_size) {
            if (auto_slim) {
                /* 自动模式（完整覆盖/自定义批量）：此槽位原版太小，跳过不影响其他槽位 */
                nf_printf("\n[NaoFu] 提示：此槽位原版字体较小（%ld 字节），瘦身后仍超出，已自动跳过。\n", search_size);
                free(vromfs_data);
                free(search_data);
                free(replace_data_padded);
                return 0;
            }
            nf_printf("\n[NaoFu] 瘦身后仍大于原版，无法替换。请换用更小的字体或联系作者。\n");
            free(vromfs_data);
            free(search_data);
            free(replace_data_padded);
            return -1;
        }
    } else {
        /* 不瘦身时也调用工具：仅做西里尔 Т/У/Е → 拉丁 T/U/E 映射，解决苏系载具名称缺字 */
        nf_printf("[NaoFu] 正在调用工具（西里尔 T/U/E 映射）...\n");
        if (NF_createDir(project_dir) != 0) {
            nf_printf("[NaoFu] 无法创建项目目录 %s。\n", project_dir);
            free(vromfs_data);
            free(search_data);
            free(replace_data_padded);
            return -1;
        }
        if (NF_runSubsetTool(NULL, REPLACE_FILE_PATH, slim_font_path) != 0) {
            nf_printf("[NaoFu] 工具执行失败。请确保 nf_subset_tool.exe 与主程序在同一目录。\n");
            free(vromfs_data);
            free(search_data);
            free(replace_data_padded);
            return -1;
        }
        nf_printf("[NaoFu] 映射完成，正在读取字体...\n");
        free(replace_data_padded);
        replace_data_padded = NULL;
        if (NF_readFileToBuffer(slim_font_path, &replace_data_padded, &replace_size) != 0) {
            nf_printf("[NaoFu] 读取字体失败。\n");
            free(vromfs_data);
            free(search_data);
            return -1;
        }
    }

    if (replace_size < search_size) {
        NF_file_size_t padding_size = search_size - replace_size;
        nf_printf("\n[NaoFu] 提示：您的字体较小，将自动填充 %ld 字节的'00'以匹配大小。\n", padding_size);
        {
            NF_byte_t* temp = (NF_byte_t*)realloc(replace_data_padded, search_size);
            if (!temp) {
                nf_printf("[NaoFu] 错误：为填充数据重新分配内存失败！\n");
                free(vromfs_data);
                free(search_data);
                free(replace_data_padded);
                return -1;
            }
            replace_data_padded = temp;
            memset(replace_data_padded + replace_size, 0, padding_size);
        }
        nf_printf("[NaoFu] 填充成功！\n");
    }

    nf_printf("\n[NaoFu] 正在定位目标字体区域 (在 fonts.vromfs.bin 文件中查找旧文件数据)...\n");
    {
        const NF_byte_t* found_address = NF_findSubsequence(vromfs_data, vromfs_size, search_data, search_size);
        if (found_address == NULL) {
            nf_printf("\n[NaoFu] 替换失败！在当前 bin 文件中未能找到该字体的原版数据块。\n");
            nf_printf("[NaoFu] 可能原因：\n");
            nf_printf("  1. 若从官方 bin 读取：font/source/fonts.vromfs.bin 不是官方原版，请看 README 进群联系作者。\n");
            nf_printf("  2. font/source/fonts.vromfs.bin_u 目录中的文件与当前 .bin 不匹配。\n");
            nf_printf("[NaoFu] 是否使用未修改的源 bin 文件 (%s) 重试？(y/n): ", SOURCE_BIN_PATH);
            {
                char ch;
                scanf(" %c", &ch);
                while (getchar() != '\n') { /* 清掉本行剩余输入 */ }
                if (ch != 'y' && ch != 'Y') {
                    free(vromfs_data);
                    free(search_data);
                    free(replace_data_padded);
                    return -1;
                }
            }
            free(vromfs_data);
            vromfs_data = NULL;
            nf_printf("[NaoFu] 正在读取源 bin 文件...\n");
            if (NF_readFileToBuffer(SOURCE_BIN_PATH, &vromfs_data, &vromfs_size) != 0) {
                nf_printf("[NaoFu] 读取源 bin 失败，请确保 %s 存在且未损坏。\n", SOURCE_BIN_PATH);
                free(search_data);
                free(replace_data_padded);
                return -1;
            }
            nf_printf("[NaoFu] 已读取源 bin，正在重新定位...\n");
            found_address = NF_findSubsequence(vromfs_data, vromfs_size, search_data, search_size);
            if (found_address == NULL) {
                nf_printf("\n[NaoFu] 使用源 bin 后仍未找到数据块，请检查 bin 与 bin_u 是否对应同一版本。\n");
                free(vromfs_data);
                free(search_data);
                free(replace_data_padded);
                return -1;
            }
        }
        {
            size_t found_offset = found_address - vromfs_data;
            nf_printf("[NaoFu] 成功定位！数据块起始偏移: %zu\n", found_offset);
            nf_printf("\n[NaoFu] 正在执行替换操作...\n");
            memcpy(vromfs_data + found_offset, replace_data_padded, search_size);
        }
    }

    nf_printf("[NaoFu] 正在缝合 (保存新文件)...\n");
    if (NF_createDir(project_dir) == 0) {
        if (NF_writeBufferToFile(output_bin_path, vromfs_data, vromfs_size) != 0) {
            nf_printf("[NaoFu] 错误：无法创建或写入最终的 fonts.vromfs.bin 文件！请检查权限，或查看 README.md\n");
        } else {
            nf_printf("\n*** [NaoFu] 字体替换成功！ ***\n\n");
            nf_printf("新的文件已保存到: ");
            printf("%s\n", output_bin_path);
            nf_printf("现在，你可以将这个文件复制到游戏的 `War Thunder/ui` 目录下替换原版文件了。\n");
            nf_printf("（注意：替换前请备份你自己的原版文件！）\n");
        }
    } else {
        nf_printf("[NaoFu] 错误：无法创建项目输出目录 ");
        printf("%s\n", project_dir);
    }

    free(vromfs_data);
    free(search_data);
    free(replace_data_padded);
    return 0;
}
