"""Builds the three Shader Graph assets of the final VFX demo, node by node.

Run:  python build_shadergraphs.py
Writes .shadergraph files into ../Shaders. Object ids are derived from stable names,
so re-running produces the same file (clean diffs).
"""
import json
import os
import uuid

OUT_DIR = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "Shaders"))
NS = uuid.UUID("6f1c2d3e-4a5b-4c6d-8e7f-90a1b2c3d4e5")

SG = "UnityEditor.ShaderGraph."
SGI = "UnityEditor.ShaderGraph.Internal."
URP = "UnityEditor.Rendering.Universal.ShaderGraph."

ZERO_MAT = {f"e{r}{c}": 0.0 for r in range(4) for c in range(4)}
IDENT_MAT = {f"e{r}{c}": (1.0 if r == c else 0.0) for r in range(4) for c in range(4)}

# URP enums
SURFACE_TRANSPARENT = 1
ALPHA_BLEND, ALPHA_ADDITIVE = 0, 2
FACE_BOTH, FACE_FRONT = 0, 2


def v4(x=0.0, y=0.0, z=0.0, w=0.0):
    return {"x": float(x), "y": float(y), "z": float(z), "w": float(w)}


class Graph:
    def __init__(self, name):
        self.name = name
        self.objects = []
        self.props = []
        self.nodes = []
        self.edges = []
        self.vertex_blocks = []
        self.fragment_blocks = []
        self._counter = 0
        self.col = {}

    # ------------------------------------------------------------ ids
    def oid(self, tag):
        self._counter += 1
        return uuid.uuid5(NS, f"{self.name}/{tag}/{self._counter}").hex

    def _add(self, obj):
        self.objects.append(obj)
        return obj["m_ObjectId"]

    # ------------------------------------------------------------ slots
    def _slot(self, typ, sid, name, slot_type, out_name=None, stage=3, **extra):
        obj = {
            "m_SGVersion": 0, "m_Type": SG + typ, "m_ObjectId": self.oid("slot"),
            "m_Id": sid, "m_DisplayName": name, "m_SlotType": slot_type, "m_Hidden": False,
            "m_ShaderOutputName": out_name or name, "m_StageCapability": stage,
        }
        obj.update(extra)
        return self._add(obj)

    def s_v1(self, sid, name, io, val=0.0, out_name=None, stage=3):
        return self._slot("Vector1MaterialSlot", sid, name, io, out_name, stage,
                          m_Value=float(val), m_DefaultValue=float(val), m_Labels=[])

    def s_v2(self, sid, name, io, x=0.0, y=0.0, out_name=None):
        val = {"x": float(x), "y": float(y)}
        return self._slot("Vector2MaterialSlot", sid, name, io, out_name,
                          m_Value=val, m_DefaultValue=dict(val), m_Labels=["X", "Y"])

    def s_v4(self, sid, name, io, out_name=None, stage=3, val=None):
        val = val or v4()
        return self._slot("Vector4MaterialSlot", sid, name, io, out_name, stage,
                          m_Value=val, m_DefaultValue=dict(val), m_Labels=[])

    def s_dynvec(self, sid, name, io, val=None):
        val = val or v4()
        return self._slot("DynamicVectorMaterialSlot", sid, name, io,
                          m_Value=val, m_DefaultValue=dict(val))

    def s_dynval(self, sid, name, io, fill=0.0):
        val = {k: float(fill) for k in ZERO_MAT}
        return self._slot("DynamicValueMaterialSlot", sid, name, io,
                          m_Value=val, m_DefaultValue=dict(IDENT_MAT))

    def s_uv(self, sid, name="UV"):
        return self._slot("UVMaterialSlot", sid, name, 0,
                          m_Value={"x": 0.0, "y": 0.0}, m_DefaultValue={"x": 0.0, "y": 0.0},
                          m_Labels=[], m_Channel=0)

    # ------------------------------------------------------------ properties
    def _prop(self, typ, version, display, ref, extra):
        obj = {
            "m_SGVersion": version, "m_Type": SGI + typ, "m_ObjectId": self.oid("prop"),
            "m_Guid": {"m_GuidSerialized": str(uuid.uuid5(NS, f"{self.name}/guid/{ref}"))},
            "m_Name": display, "m_DefaultRefNameVersion": 1,
            "m_RefNameGeneratedByDisplayName": display,
            "m_DefaultReferenceName": "_" + display.replace(" ", "_"),
            "m_OverrideReferenceName": ref, "m_GeneratePropertyBlock": True,
            "m_UseCustomSlotLabel": False, "m_CustomSlotLabel": "", "m_DismissedVersion": 0,
            "m_Precision": 0, "overrideHLSLDeclaration": False, "hlslDeclarationOverride": 0,
            "m_Hidden": False, "m_PerRendererData": False, "m_customAttributes": [],
        }
        obj.update(extra)
        self.props.append(obj["m_ObjectId"])
        self._add(obj)
        return obj

    def p_float(self, display, ref, value, slider=None):
        return self._prop("Vector1ShaderProperty", 1, display, ref, {
            "m_Value": float(value), "m_FloatType": 1 if slider else 0,
            "m_RangeValues": {"x": float(slider[0]), "y": float(slider[1])} if slider else {"x": 0.0, "y": 1.0},
            "m_SliderType": 0, "m_SliderPower": 3.0, "m_EnumType": 0, "m_CSharpEnumString": "",
            "m_EnumNames": ["Default"], "m_EnumValues": [0]})

    def p_vec2(self, display, ref, x, y):
        return self._prop("Vector2ShaderProperty", 1, display, ref, {"m_Value": v4(x, y)})

    def p_color(self, display, ref, r, g, b, a=1.0, hdr=True):
        return self._prop("ColorShaderProperty", 3, display, ref, {
            "m_Value": {"r": float(r), "g": float(g), "b": float(b), "a": float(a)},
            "isMainColor": False, "m_ColorMode": 1 if hdr else 0})

    def p_tex(self, display, ref, main=False):
        return self._prop("Texture2DShaderProperty", 0, display, ref, {
            "m_Value": {"m_SerializedTexture": "", "m_Guid": ""},
            "isMainTexture": main, "useTilingAndOffset": False, "useTexelSize": True,
            "m_Modifiable": True, "m_DefaultType": 0})

    # ------------------------------------------------------------ nodes
    def _node(self, typ, name, slots, pos, version=0, synonyms=None, **extra):
        obj = {
            "m_SGVersion": version, "m_Type": SG + typ, "m_ObjectId": self.oid("node"),
            "m_Group": {"m_Id": ""}, "m_Name": name,
            "m_DrawState": {"m_Expanded": True, "m_Position": {
                "serializedVersion": "2", "x": float(pos[0]), "y": float(pos[1]),
                "width": 208.0, "height": 300.0}},
            "m_Slots": [{"m_Id": s} for s in slots], "synonyms": synonyms or [],
            "m_Precision": 0, "m_PreviewExpanded": False, "m_DismissedVersion": 0,
            "m_PreviewMode": 0, "m_CustomColors": {"m_SerializableColors": []},
        }
        obj.update(extra)
        self.nodes.append(obj["m_ObjectId"])
        return self._add(obj)

    def prop_node(self, prop, pos):
        t = prop["m_Type"].split(".")[-1]
        name = prop["m_Name"]
        if t == "Vector1ShaderProperty":
            s = self.s_v1(0, name, 1, out_name="Out")
        elif t == "Vector2ShaderProperty":
            s = self.s_v2(0, name, 1, out_name="Out")
        elif t == "ColorShaderProperty":
            s = self.s_v4(0, name, 1, out_name="Out")
        else:
            s = self._slot("Texture2DMaterialSlot", 0, name, 1, "Out", m_BareResource=False)
        return self._node("PropertyNode", "Property", [s], pos, m_Property={"m_Id": prop["m_ObjectId"]})

    def uv(self, pos):
        return self._node("UVNode", "UV", [self.s_v4(0, "Out", 1)], pos, m_OutputChannel=0)

    def time(self, pos):
        slots = [self.s_v1(i, n, 1) for i, n in enumerate(
            ["Time", "Sine Time", "Cosine Time", "Delta Time", "Smooth Delta"])]
        return self._node("TimeNode", "Time", slots, pos)

    def vertex_color(self, pos):
        return self._node("VertexColorNode", "Vertex Color", [self.s_v4(0, "Out", 1, val=v4(1, 1, 1, 1))], pos)

    def binary(self, kind, pos, a=0.0, b=0.0):
        if kind == "Multiply":
            slots = [self.s_dynval(0, "A", 0, a), self.s_dynval(1, "B", 0, b), self.s_dynval(2, "Out", 1)]
        else:
            slots = [self.s_dynvec(0, "A", 0, v4(a, a, a, a)), self.s_dynvec(1, "B", 0, v4(b, b, b, b)),
                     self.s_dynvec(2, "Out", 1)]
        return self._node(kind + "Node", kind, slots, pos)

    def mul(self, pos, b=1.0):
        return self.binary("Multiply", pos, 0.0, b)

    def add(self, pos):
        return self.binary("Add", pos)

    def sub(self, pos, b=0.0):
        return self.binary("Subtract", pos, 0.0, b)

    def unary(self, kind, pos, default_in=0.0):
        slots = [self.s_dynvec(0, "In", 0, v4(default_in, default_in, default_in, default_in)),
                 self.s_dynvec(1, "Out", 1)]
        return self._node(kind + "Node", kind.replace("OneMinus", "One Minus"), slots, pos)

    def step(self, pos):
        slots = [self.s_dynvec(0, "Edge", 0, v4(1, 1, 1, 1)), self.s_dynvec(1, "In", 0),
                 self.s_dynvec(2, "Out", 1)]
        return self._node("StepNode", "Step", slots, pos)

    def lerp(self, pos):
        slots = [self.s_dynvec(0, "A", 0), self.s_dynvec(1, "B", 0, v4(1, 1, 1, 1)),
                 self.s_dynvec(2, "T", 0), self.s_dynvec(3, "Out", 1)]
        return self._node("LerpNode", "Lerp", slots, pos)

    def split(self, pos):
        slots = [self.s_dynvec(0, "In", 0)] + [self.s_v1(i + 1, c, 1) for i, c in enumerate("RGBA")]
        return self._node("SplitNode", "Split", slots, pos)

    def tiling_offset(self, pos, tiling=(1.0, 1.0)):
        slots = [self.s_uv(0), self.s_v2(1, "Tiling", 0, *tiling), self.s_v2(2, "Offset", 0),
                 self.s_v2(3, "Out", 1)]
        return self._node("TilingAndOffsetNode", "Tiling And Offset", slots, pos)

    def simple_noise(self, pos, scale=50.0):
        slots = [self.s_uv(0), self.s_v1(1, "Scale", 0, scale), self.s_v1(2, "Out", 1)]
        return self._node("NoiseNode", "Simple Noise", slots, pos, version=1, m_HashType=0)

    def sample_tex(self, pos):
        slots = [
            self.s_v4(0, "RGBA", 1, stage=2),
            self.s_v1(4, "R", 1, stage=2), self.s_v1(5, "G", 1, stage=2),
            self.s_v1(6, "B", 1, stage=2), self.s_v1(7, "A", 1, stage=2),
            self._slot("Texture2DInputMaterialSlot", 1, "Texture", 0, m_BareResource=False,
                       m_Texture={"m_SerializedTexture": "", "m_Guid": ""}, m_DefaultType=0),
            self.s_uv(2),
            self._slot("SamplerStateMaterialSlot", 3, "Sampler", 0, m_BareResource=False),
        ]
        return self._node("SampleTexture2DNode", "Sample Texture 2D", slots, pos,
                          m_TextureType=0, m_NormalMapSpace=0, m_EnableGlobalMipBias=True,
                          m_MipSamplingMode=0)

    # ------------------------------------------------------------ blocks
    def _block(self, descriptor, slot):
        obj = {
            "m_SGVersion": 0, "m_Type": SG + "BlockNode", "m_ObjectId": self.oid("block"),
            "m_Group": {"m_Id": ""}, "m_Name": descriptor,
            "m_DrawState": {"m_Expanded": True, "m_Position": {
                "serializedVersion": "2", "x": 0.0, "y": 0.0, "width": 0.0, "height": 0.0}},
            "m_Slots": [{"m_Id": slot}], "synonyms": [], "m_Precision": 0,
            "m_PreviewExpanded": True, "m_DismissedVersion": 0, "m_PreviewMode": 0,
            "m_CustomColors": {"m_SerializableColors": []}, "m_SerializedDescriptor": descriptor,
        }
        self.nodes.append(obj["m_ObjectId"])
        return self._add(obj)

    def default_blocks(self, clip_threshold=0.5):
        z3 = {"x": 0.0, "y": 0.0, "z": 0.0}
        for name, typ in (("Position", "PositionMaterialSlot"), ("Normal", "NormalMaterialSlot"),
                          ("Tangent", "TangentMaterialSlot")):
            s = self._slot(typ, 0, name, 0, stage=1, m_Value=dict(z3), m_DefaultValue=dict(z3),
                           m_Labels=[], m_Space=0)
            self.vertex_blocks.append(self._block("VertexDescription." + name, s))
        s = self._slot("ColorRGBMaterialSlot", 0, "Base Color", 0, "BaseColor", stage=2,
                       m_Value={"x": 0.5, "y": 0.5, "z": 0.5}, m_DefaultValue={"x": 0.5, "y": 0.5, "z": 0.5},
                       m_Labels=[], m_ColorMode=0, m_DefaultColor={"r": 0.5, "g": 0.5, "b": 0.5, "a": 1.0})
        self.base_color = self._block("SurfaceDescription.BaseColor", s)
        s = self.s_v1(0, "Alpha", 0, 1.0, stage=2)
        self.alpha = self._block("SurfaceDescription.Alpha", s)
        s = self.s_v1(0, "Alpha Clip Threshold", 0, clip_threshold, out_name="AlphaClipThreshold", stage=2)
        self.alpha_clip = self._block("SurfaceDescription.AlphaClipThreshold", s)
        self.fragment_blocks += [self.base_color, self.alpha, self.alpha_clip]

    # ------------------------------------------------------------ edges
    def link(self, out_node, out_slot, in_node, in_slot):
        self.edges.append({"m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
                           "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot}})

    # ------------------------------------------------------------ output
    def write(self, surface, alpha_mode, render_face, alpha_clip, path="Final VFX"):
        sub = {"m_SGVersion": 2, "m_Type": URP + "UniversalUnlitSubTarget", "m_ObjectId": self.oid("sub")}
        target = {
            "m_SGVersion": 1, "m_Type": URP + "UniversalTarget", "m_ObjectId": self.oid("target"),
            "m_Datas": [], "m_ActiveSubTarget": {"m_Id": sub["m_ObjectId"]},
            "m_AllowMaterialOverride": False, "m_SurfaceType": surface, "m_ZTestMode": 4,
            "m_ZWriteControl": 0, "m_AlphaMode": alpha_mode, "m_RenderFace": render_face,
            "m_AlphaClip": alpha_clip, "m_CastShadows": False, "m_ReceiveShadows": False,
            "m_DisableTint": False, "m_AdditionalMotionVectorMode": 0, "m_AlembicMotionVectors": False,
            "m_SupportsLODCrossFade": False, "m_CustomEditorGUI": "", "m_SupportVFX": False,
        }
        category = {"m_SGVersion": 0, "m_Type": SG + "CategoryData", "m_ObjectId": self.oid("cat"),
                    "m_Name": "", "m_ChildObjectList": [{"m_Id": p} for p in self.props]}
        graph = {
            "m_SGVersion": 3, "m_Type": SG + "GraphData", "m_ObjectId": self.oid("graph"),
            "m_Properties": [{"m_Id": p} for p in self.props], "m_Keywords": [], "m_Dropdowns": [],
            "m_CategoryData": [{"m_Id": category["m_ObjectId"]}],
            "m_Nodes": [{"m_Id": n} for n in self.nodes], "m_GroupDatas": [], "m_StickyNoteDatas": [],
            "m_Edges": self.edges,
            "m_VertexContext": {"m_Position": {"x": 900.0, "y": -200.0},
                                "m_Blocks": [{"m_Id": b} for b in self.vertex_blocks]},
            "m_FragmentContext": {"m_Position": {"x": 900.0, "y": 100.0},
                                  "m_Blocks": [{"m_Id": b} for b in self.fragment_blocks]},
            "m_PreviewData": {"serializedMesh": {
                "m_SerializedMesh": "{\"mesh\":{\"fileID\":10210,\"guid\":\"0000000000000000e000000000000000\",\"type\":0}}",
                "m_Guid": ""}, "preventRotation": False},
            "m_Path": path, "m_GraphPrecision": 1, "m_PreviewMode": 2, "m_OutputNode": {"m_Id": ""},
            "m_SubDatas": [], "m_ActiveTargets": [{"m_Id": target["m_ObjectId"]}],
        }
        parts = [graph, category, target, sub] + self.objects
        os.makedirs(OUT_DIR, exist_ok=True)
        out = os.path.join(OUT_DIR, self.name + ".shadergraph")
        with open(out, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n\n".join(json.dumps(p, indent=4) for p in parts) + "\n\n")
        print("wrote", out)


# ====================================================================== LASER
def build_laser():
    g = Graph("SG_LaserBeam")
    p_mask = g.p_tex("Beam Mask (RGB)", "_BeamMask", main=True)
    p_core = g.p_color("Core Color", "_CoreColor", 6.2, 2.9, 5.6)       # magenta-white, ~2.6 stops
    p_glow = g.p_color("Glow Color", "_GlowColor", 1.15, 0.22, 2.3)     # violet
    p_edge = g.p_color("Edge Color", "_EdgeColor", 0.12, 1.5, 1.7)      # cyan rims
    p_int = g.p_float("Intensity", "_Intensity", 1.0)
    p_scroll = g.p_vec2("Scroll Speed", "_ScrollSpeed", -2.2, 0.0)
    p_nscale = g.p_float("Noise Scale", "_NoiseScale", 42.0)
    p_nspeed = g.p_vec2("Noise Scroll Speed", "_NoiseScrollSpeed", -3.4, 0.6)
    p_dist = g.p_float("Distortion Strength", "_DistortionStrength", 0.045, slider=(0.0, 0.2))
    p_diss = g.p_float("Dissolve", "_Dissolve", 0.0, slider=(0.0, 1.0))
    g.default_blocks()

    uv = g.uv((-1900, 0))
    t = g.time((-1900, -400))
    n_scroll = g.prop_node(p_scroll, (-1900, -150))
    n_nspeed = g.prop_node(p_nspeed, (-1900, 300))

    # scrolling UV for the mask
    m_scroll = g.mul((-1650, -300))
    g.link(t, 0, m_scroll, 0); g.link(n_scroll, 0, m_scroll, 1)
    to_main = g.tiling_offset((-1400, -200))
    g.link(uv, 0, to_main, 0); g.link(m_scroll, 2, to_main, 2)

    # noise with its own scroll, used for distortion and dissolve
    m_nscroll = g.mul((-1650, 300))
    g.link(t, 0, m_nscroll, 0); g.link(n_nspeed, 0, m_nscroll, 1)
    to_noise = g.tiling_offset((-1400, 300), tiling=(1.0, 1.0))
    g.link(uv, 0, to_noise, 0); g.link(m_nscroll, 2, to_noise, 2)
    noise = g.simple_noise((-1150, 300))
    g.link(to_noise, 3, noise, 0)
    g.link(g.prop_node(p_nscale, (-1400, 550)), 0, noise, 1)

    centered = g.sub((-900, 150), b=0.5)
    g.link(noise, 2, centered, 0)
    distort = g.mul((-700, 150))
    g.link(centered, 2, distort, 0); g.link(g.prop_node(p_dist, (-900, 350)), 0, distort, 1)
    uv_final = g.add((-1100, -200))
    g.link(to_main, 3, uv_final, 0); g.link(distort, 2, uv_final, 1)

    tex = g.sample_tex((-850, -300))
    g.link(g.prop_node(p_mask, (-1100, -400)), 0, tex, 1); g.link(uv_final, 2, tex, 2)

    # R/G/B masks tinted with their own HDR colors
    c_core = g.mul((-550, -650)); g.link(tex, 4, c_core, 0); g.link(g.prop_node(p_core, (-800, -700)), 0, c_core, 1)
    c_glow = g.mul((-550, -450)); g.link(tex, 5, c_glow, 0); g.link(g.prop_node(p_glow, (-800, -500)), 0, c_glow, 1)
    c_edge = g.mul((-550, -250)); g.link(tex, 6, c_edge, 0); g.link(g.prop_node(p_edge, (-800, -100)), 0, c_edge, 1)
    a1 = g.add((-330, -550)); g.link(c_core, 2, a1, 0); g.link(c_glow, 2, a1, 1)
    a2 = g.add((-150, -450)); g.link(a1, 2, a2, 0); g.link(c_edge, 2, a2, 1)

    vc = g.vertex_color((-400, 0))
    tinted = g.mul((50, -400)); g.link(a2, 2, tinted, 0); g.link(vc, 0, tinted, 1)
    lit = g.mul((250, -350)); g.link(tinted, 2, lit, 0); g.link(g.prop_node(p_int, (50, -150)), 0, lit, 1)
    g.link(lit, 2, g.base_color, 0)

    # alpha = texture alpha * vertex alpha * dissolve mask
    vc_split = g.split((-150, 0)); g.link(vc, 0, vc_split, 0)
    alpha = g.mul((50, 0)); g.link(tex, 7, alpha, 0); g.link(vc_split, 4, alpha, 1)
    dissolve = g.step((-150, 300))
    g.link(g.prop_node(p_diss, (-400, 300)), 0, dissolve, 0); g.link(noise, 2, dissolve, 1)
    alpha2 = g.mul((250, 100)); g.link(alpha, 2, alpha2, 0); g.link(dissolve, 2, alpha2, 1)
    g.link(alpha2, 2, g.alpha, 0)

    g.write(SURFACE_TRANSPARENT, ALPHA_BLEND, FACE_BOTH, alpha_clip=False)


# ====================================================================== ORB
def build_orb():
    g = Graph("SG_OrbAdditive")
    p_tex = g.p_tex("Particle Texture", "_ParticleTex", main=True)
    p_tint = g.p_color("Tint Color", "_TintColor", 1.3, 2.4, 0.45)     # acid lime, ~1.3 stops
    p_int = g.p_float("Intensity", "_Intensity", 1.0)
    g.default_blocks()

    tex = g.sample_tex((-900, -200))
    g.link(g.prop_node(p_tex, (-1150, -200)), 0, tex, 1)
    vc = g.vertex_color((-900, 200))
    m1 = g.mul((-600, -100)); g.link(tex, 0, m1, 0); g.link(vc, 0, m1, 1)
    m2 = g.mul((-400, -100)); g.link(m1, 2, m2, 0); g.link(g.prop_node(p_tint, (-600, 150)), 0, m2, 1)
    m3 = g.mul((-200, -200)); g.link(m2, 2, m3, 0); g.link(g.prop_node(p_int, (-400, 200)), 0, m3, 1)
    g.link(m3, 2, g.base_color, 0)
    sp = g.split((-200, 100)); g.link(m2, 2, sp, 0)
    sat = g.unary("Saturate", (0, 100)); g.link(sp, 4, sat, 0)
    g.link(sat, 1, g.alpha, 0)

    g.write(SURFACE_TRANSPARENT, ALPHA_ADDITIVE, FACE_BOTH, alpha_clip=False)


# ====================================================================== TRAIL
def build_trail(name="SG_StylizedTrail"):
    """Trail shader: HDR color gradient (Lerp on UV.r), scrolling streak texture,
    fade towards the tail that is broken up by a scrolling Simple Noise plus the inverted
    gradient. Two-sided, transparent, alpha clipping."""
    g = Graph(name)
    p_tex = g.p_tex("Streak Texture", "_StreakTex", main=True)
    p_head = g.p_color("Head Color", "_HeadColor", 3.4, 0.95, 0.55)    # coral
    p_tail = g.p_color("Tail Color", "_TailColor", 0.1, 1.25, 1.1)     # teal
    p_int = g.p_float("Intensity", "_Intensity", 1.0)
    p_tiling = g.p_vec2("Streak Tiling", "_StreakTiling", 1.5, 1.0)
    p_scroll = g.p_vec2("Streak Scroll Speed", "_StreakScroll", -0.65, 0.0)
    p_offset = g.p_vec2("Scroll Offset", "_ScrollOffset", 0.0, 0.0)  # extra offset driven by script
    p_nscale = g.p_float("Dissolve Noise Scale", "_NoiseScale", 24.0)
    p_nspeed = g.p_vec2("Dissolve Scroll Speed", "_NoiseScrollSpeed", -0.8, 0.15)
    p_diss = g.p_float("Dissolve", "_Dissolve", 0.0, slider=(0.0, 2.0))
    p_clip = g.p_float("Alpha Clip", "_AlphaClip", 0.1, slider=(0.0, 1.0))
    g.default_blocks(clip_threshold=0.1)

    uv = g.uv((-2100, 0))
    uv_split = g.split((-1850, -500)); g.link(uv, 0, uv_split, 0)
    t = g.time((-2100, -300))

    # 1. color gradient: Lerp(head, tail, UV.r)
    grad = g.lerp((-900, -800))
    g.link(g.prop_node(p_head, (-1150, -900)), 0, grad, 0)
    g.link(g.prop_node(p_tail, (-1150, -750)), 0, grad, 1)
    g.link(uv_split, 1, grad, 2)

    # 2. streak texture panned with Time * speed (+ script offset)
    m_scroll = g.mul((-1850, -150)); g.link(t, 0, m_scroll, 0)
    g.link(g.prop_node(p_scroll, (-2100, -150)), 0, m_scroll, 1)
    scroll_total = g.add((-1650, -150))
    g.link(m_scroll, 2, scroll_total, 0); g.link(g.prop_node(p_offset, (-1850, 50)), 0, scroll_total, 1)
    to_tex = g.tiling_offset((-1450, -150))
    g.link(uv, 0, to_tex, 0); g.link(g.prop_node(p_tiling, (-1650, 100)), 0, to_tex, 1)
    g.link(scroll_total, 2, to_tex, 2)
    tex = g.sample_tex((-1200, -150))
    g.link(g.prop_node(p_tex, (-1450, -350)), 0, tex, 1); g.link(to_tex, 3, tex, 2)

    # base color = gradient * texture * vertex color * intensity
    c0 = g.mul((-700, -650)); g.link(grad, 3, c0, 0); g.link(tex, 4, c0, 1)
    vc = g.vertex_color((-900, -400))
    c1 = g.mul((-500, -600)); g.link(c0, 2, c1, 0); g.link(vc, 0, c1, 1)
    c2 = g.mul((-300, -550)); g.link(c1, 2, c2, 0); g.link(g.prop_node(p_int, (-500, -350)), 0, c2, 1)
    g.link(c2, 2, g.base_color, 0)

    # 3. dissolve: (scrolling noise + inverted gradient) - gradient - Dissolve
    m_nscroll = g.mul((-1850, 400)); g.link(t, 0, m_nscroll, 0)
    g.link(g.prop_node(p_nspeed, (-2100, 450)), 0, m_nscroll, 1)
    to_noise = g.tiling_offset((-1600, 400))
    g.link(uv, 0, to_noise, 0); g.link(m_nscroll, 2, to_noise, 2)
    noise = g.simple_noise((-1350, 400))
    g.link(to_noise, 3, noise, 0); g.link(g.prop_node(p_nscale, (-1600, 650)), 0, noise, 1)
    inv = g.unary("OneMinus", (-1350, 150), default_in=1.0); g.link(uv_split, 1, inv, 0)
    brighter = g.add((-1100, 300)); g.link(noise, 2, brighter, 0); g.link(inv, 1, brighter, 1)
    fade = g.sub((-900, 300)); g.link(brighter, 2, fade, 0); g.link(uv_split, 1, fade, 1)
    dissolved = g.sub((-700, 350)); g.link(fade, 2, dissolved, 0)
    g.link(g.prop_node(p_diss, (-900, 550)), 0, dissolved, 1)

    # alpha = saturate(texture * dissolve field) * vertex alpha
    vc_split = g.split((-700, -150)); g.link(vc, 0, vc_split, 0)
    a1 = g.mul((-500, 150)); g.link(tex, 4, a1, 0); g.link(dissolved, 2, a1, 1)
    sat = g.unary("Saturate", (-300, 150)); g.link(a1, 2, sat, 0)
    a2 = g.mul((-100, 150)); g.link(sat, 1, a2, 0); g.link(vc_split, 4, a2, 1)
    g.link(a2, 2, g.alpha, 0)
    g.link(g.prop_node(p_clip, (-300, 400)), 0, g.alpha_clip, 0)

    g.write(SURFACE_TRANSPARENT, ALPHA_BLEND, FACE_BOTH, alpha_clip=True)


if __name__ == "__main__":
    build_laser()
    build_orb()
    build_trail()
