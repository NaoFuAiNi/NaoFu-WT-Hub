/* nf_ui.c - 控制台菜单 */

#include "nf_ui.h"
#include "nf_console.h"
#include "nf_fonts.h"
#include "nf_types.h"
#include <stdlib.h>

void NF_displayMenuHeader(void) {
    nf_printf("=======================================================================\n");
    nf_printf("=            NaoFu's War Thunder Custom Font Patcher v2.1.3            =\n");
    nf_printf("=                      战争雷霆自定义字体替换工具                     =\n");
    nf_printf("=======================================================================\n");
    nf_printf(" [NaoFu] 作者: NaoFu (一只小脑斧) | B站UID: 405046590 | Q群: 941587776\n");
    nf_printf("-----------------------------------------------------------------------\n");
}

void NF_displayMenuList(void) {
    u32 i;
    nf_printf("[NaoFu] 提示：请将您的字体放在 font/custom/MyFonts.ttf；若大于原版将提示是否瘦身。\n");
    nf_printf("[NaoFu] 请从以下列表中选择一个您想要替换的游戏内字体:\n");
    for (i = 0; i < g_num_font_choices; ++i) {
        nf_printf("  %-4u -> %s\n", g_font_choices[i].id, g_font_choices[i].display_name);
    }
}

void NF_displayMenu(void) {
    NF_displayMenuHeader();
    NF_displayMenuList();
}

void NF_pause(void) {
    nf_printf("\n");
    system("pause");
}


