#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
字体瘦身 + 西里尔→拉丁 remap 工具。
用法1（瘦身+remap）: subset_tool.py <参考字体.ttf> <待瘦身字体.ttf> <输出字体.ttf>
用法2（仅 remap）: subset_tool.py <输入字体.ttf> <输出字体.ttf>

瘦身策略（三参数模式）：
  第一轮：按参考字体字符集裁剪 + 深度压缩（去 hinting / layout features）+ VF 压平。
  若结果仍超过参考字体文件大小，第二轮：改用"最小拉丁集"
    (Basic Latin U+0020-U+007E + Latin-1 Supplement U+00A0-U+00FF，共约 190 字)
    从原始字体裁出，同样深度压缩，使体积能塞入极小槽位（如 53KB 的 OTF）。
  两轮结果取更小的那个写入输出。

remap: 西里尔 Т(U+0422)/У(U+0423)/Е(U+0415) 使用拉丁 T/U/E 字形，解决苏系载具名称缺字。
成功退出码 0，失败 1。
"""
import sys
import os
import tempfile

# 被 C 调起来时 stdout/stderr 可能没挂好，兜个底
def _safe_stream(name, default_fd):
    s = sys.__dict__.get(name)
    if s is not None and hasattr(s, "write"):
        return s
    try:
        return os.fdopen(default_fd, "w")
    except Exception:
        return open(os.devnull, "w")
if sys.stdout is None:
    sys.stdout = _safe_stream("stdout", 1)
if sys.stderr is None:
    sys.stderr = _safe_stream("stderr", 2)

# 苏系载具名会用西里尔 Т/У/Е，没这仨就缺字，用拉丁 T/U/E 顶
CYRILLIC_TO_LATIN = [
    (0x0422, 0x0054),  # Т → T
    (0x0423, 0x0055),  # У → U
    (0x0415, 0x0045),  # Е → E
]

# 最小拉丁集：Basic Latin + Latin-1 Supplement，约 190 字
# 用于极小槽位（参考字体 < ~100KB）的兜底压缩
MIN_LATIN_UNICODES = set(range(0x0020, 0x007F)) | set(range(0x00A0, 0x0100))

def remap_cyrillic_to_latin(font):
    """往 cmap 里加 Т/У/Е → T/U/E。只动 Unicode 子表，format 0 那种别动，不然保存会炸。"""
    cmap = font.getBestCmap()
    if not cmap:
        return

    glyph_for = {}
    for _cyr, lat in CYRILLIC_TO_LATIN:
        if lat in cmap:
            glyph_for[_cyr] = cmap[lat]
    if not glyph_for:
        return

    cmap_table = font.get("cmap")
    if not cmap_table:
        return

    for table in getattr(cmap_table, "tables", []):
        if not hasattr(table, "cmap") or not isinstance(table.cmap, dict):
            continue

        # 只改 Unicode 子表
        is_unicode = (
            table.platformID == 0
            or (table.platformID == 3 and getattr(table, "platEncID", None) in (1, 10))
        )
        if not is_unicode:
            continue

        # format 0 只支持 0~255，塞 0x0422 会炸
        fmt = getattr(table, "format", None)
        if fmt == 0:
            continue

        for cyr, gname in glyph_for.items():
            table.cmap[cyr] = gname


def _make_options(deep=False):
    """返回 subset Options，deep=True 时去掉 hinting 和 layout features。"""
    from fontTools import subset as subset_module
    opts = subset_module.Options()
    opts.prune_unicode_ranges = False
    if deep:
        opts.hinting         = False
        opts.desubroutinize  = True
        opts.layout_features = []
    return opts


def _instantiate_vf(font):
    """若是 VF，压平到默认轴静态字，去掉 gvar/fvar/HVAR 等。失败时只打 Warning。"""
    if "fvar" not in font:
        return
    try:
        from fontTools.varLib import instancer
        limits = {}
        for ax in font["fvar"].axes:
            tag = ax.axisTag
            if isinstance(tag, bytes):
                tag = tag.decode("ascii", "replace")
            limits[str(tag).strip()] = None
        instancer.instantiateVariableFont(font, limits, inplace=True)
    except Exception as e:
        import traceback
        sys.stderr.write("Warning: VF instantiation failed, continuing without it: %s\n" % str(e))
        traceback.print_exc(file=sys.stderr)


def _subset_and_save(input_path, unicodes, deep, output_path):
    """从 input_path 按 unicodes 裁剪（deep 时深度压缩），保存到 output_path，返回文件大小。"""
    from fontTools.ttLib import TTFont
    from fontTools import subset as subset_module
    font = TTFont(input_path)
    opts = _make_options(deep=deep)
    sub = subset_module.Subsetter(opts)
    sub.populate(unicodes=unicodes)
    sub.subset(font)
    _instantiate_vf(font)
    remap_cyrillic_to_latin(font)
    font.save(output_path)
    font.close()
    return os.path.getsize(output_path)


def main():
    argc = len(sys.argv)
    if argc == 3:
        # 只做 remap
        input_path = sys.argv[1]
        output_path = sys.argv[2]
        ref_path = None
    elif argc == 4:
        # 瘦身 + remap
        ref_path = sys.argv[1]
        input_path = sys.argv[2]
        output_path = sys.argv[3]
    else:
        sys.stderr.write("Usage: subset_tool.py <input_font> <output_font>  (remap only)\n")
        sys.stderr.write("   or: subset_tool.py <ref_font> <input_font> <output_font>  (subset + remap)\n")
        sys.exit(1)

    if not os.path.isfile(input_path):
        sys.stderr.write("Error: input font not found: %s\n" % input_path)
        sys.exit(1)
    if ref_path is not None and not os.path.isfile(ref_path):
        sys.stderr.write("Error: ref font not found: %s\n" % ref_path)
        sys.exit(1)

    try:
        from fontTools.ttLib import TTFont
        from fontTools import subset as subset_module
    except ImportError:
        sys.stderr.write("Error: fonttools not found (bundled exe should include it)\n")
        sys.exit(1)

    try:
        if ref_path is not None:
            # ── 三参数：瘦身 + remap ──────────────────────────────────────
            ref = TTFont(ref_path)
            rcmap = ref.getBestCmap()
            ref.close()
            if not rcmap:
                sys.stderr.write("Error: ref font has no cmap\n")
                sys.exit(1)

            ref_size    = os.path.getsize(ref_path)
            ref_unicodes = set(rcmap.keys())

            # 第一轮：按参考字符集深度压缩
            tmp1 = tempfile.NamedTemporaryFile(suffix=".ttf", delete=False)
            tmp1.close()
            try:
                size1 = _subset_and_save(input_path, ref_unicodes, deep=True, output_path=tmp1.name)

                if size1 <= ref_size:
                    # 第一轮就够了，直接用
                    import shutil
                    shutil.move(tmp1.name, output_path)
                    sys.stderr.write("Info: round1 subset ok (%d bytes, ref %d bytes)\n" % (size1, ref_size))
                else:
                    # 第一轮仍超出 → 第二轮：最小拉丁集，从原始字体裁
                    sys.stderr.write(
                        "Info: round1 too large (%d > %d), trying min-Latin fallback\n" % (size1, ref_size)
                    )
                    # 与用户字体实际有的字符取交集
                    icmap = TTFont(input_path).getBestCmap()
                    TTFont(input_path).close()
                    min_unicodes = MIN_LATIN_UNICODES & set(icmap.keys())

                    tmp2 = tempfile.NamedTemporaryFile(suffix=".ttf", delete=False)
                    tmp2.close()
                    try:
                        size2 = _subset_and_save(input_path, min_unicodes, deep=True, output_path=tmp2.name)
                        sys.stderr.write("Info: round2 min-Latin size = %d bytes\n" % size2)
                        # 取两轮中更小的
                        if size2 <= size1:
                            import shutil
                            shutil.move(tmp2.name, output_path)
                        else:
                            import shutil
                            shutil.move(tmp1.name, output_path)
                    finally:
                        if os.path.exists(tmp2.name):
                            os.unlink(tmp2.name)

            finally:
                if os.path.exists(tmp1.name):
                    os.unlink(tmp1.name)

        else:
            # ── 两参数：仅 remap ──────────────────────────────────────────
            font = TTFont(input_path)
            remap_cyrillic_to_latin(font)
            font.save(output_path)
            font.close()

    except Exception as e:
        import traceback
        sys.stderr.write("Error: %s\n" % str(e))
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)
    sys.exit(0)

if __name__ == "__main__":
    main()
