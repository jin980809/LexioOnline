"""Procedural LEXIO game table (Blender bpy). Units: meters, Z-up in Blender (exporters convert for Unity)."""
import bpy, bmesh, math, os, sys
from mathutils import Vector

ROOT = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(ROOT, "textures_toon")
OUT = os.path.join(ROOT, "export_toon")
os.makedirs(OUT, exist_ok=True)
SEG = 256

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- materials
def principled(name):
    m = bpy.data.materials.new(name)
    try: m.use_nodes = True
    except Exception: pass
    return m, m.node_tree.nodes["Principled BSDF"], m.node_tree

def tex_node(nt, file, noncolor=False):
    n = nt.nodes.new("ShaderNodeTexImage")
    n.image = bpy.data.images.load(os.path.join(TEX, file))
    if noncolor: n.image.colorspace_settings.name = "Non-Color"
    return n

def add_normal(nt, bsdf, file, strength=1.0):
    t = tex_node(nt, file, True)
    nm = nt.nodes.new("ShaderNodeNormalMap"); nm.inputs["Strength"].default_value = strength
    nt.links.new(t.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])

def srgb(r, g, b):
    f = lambda v: (v / 255) / 12.92 if v / 255 <= 0.04045 else ((v / 255 + 0.055) / 1.055) ** 2.4
    return (f(r), f(g), f(b), 1)

def toon(m_b, rough=0.6):
    m_b.inputs["Metallic"].default_value = 0.0
    m_b.inputs["Roughness"].default_value = rough
    m_b.inputs["Specular IOR Level"].default_value = 0.25

M_FELT, b, nt = principled("M_Felt")
nt.links.new(tex_node(nt, "Felt_BaseColor.png").outputs["Color"], b.inputs["Base Color"])
add_normal(nt, b, "Felt_Normal.png", 0.6)
toon(b, 0.85)

M_BLUE, b, nt = principled("M_BluePanel")
nt.links.new(tex_node(nt, "BluePanel_BaseColor.png").outputs["Color"], b.inputs["Base Color"])
add_normal(nt, b, "BluePanel_Normal.png", 0.8)
toon(b, 0.5)

M_CREAM, b, _ = principled("M_Cream"); b.inputs["Base Color"].default_value = srgb(255, 229, 140); toon(b, 0.55)
M_YELLOW, b, _ = principled("M_Yellow"); b.inputs["Base Color"].default_value = srgb(250, 196, 58); toon(b, 0.45)

# ---------------------------------------------------------------- geometry
def lathe(name, prof, mat, u_scale=1.0, a0=0.0, a1=2 * math.pi, closed_profile=False, planar_uv=None, parent=None):
    """prof: list of (r, z), ordered so the surface faces outward (top: r+, outer wall: z-, bottom: r-)."""
    full = abs(a1 - a0 - 2 * math.pi) < 1e-6
    n = max(4, int(SEG * (a1 - a0) / (2 * math.pi)))
    p = list(prof) + ([prof[0]] if closed_profile else [])
    L = [0.0]
    for i in range(1, len(p)):
        L.append(L[-1] + math.dist(p[i], p[i - 1]))
    verts, uvs, faces = [], [], []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        for j, (r, z) in enumerate(p):
            x, y = r * math.cos(a), r * math.sin(a)
            verts.append((x, y, z))
            if planar_uv: uvs.append((0.5 + x / (2 * planar_uv), 0.5 + y / (2 * planar_uv)))
            else: uvs.append((u_scale * i / n * (a1 - a0) / (2 * math.pi), L[j] / L[-1]))
    m = len(p)
    for i in range(n):
        for j in range(m - 1):
            faces.append((i * m + j, i * m + j + 1, (i + 1) * m + j + 1, (i + 1) * m + j))
    if not full and closed_profile:      # end caps
        faces.append(tuple(reversed(range(0, m - 1))))
        faces.append(tuple(n * m + j for j in range(0, m - 1)))
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            uvl.data[li].uv = uvs[me.loops[li].vertex_index]
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    bm = bmesh.new(); bm.from_mesh(me)
    if full: bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6) if planar_uv else None
    if closed_profile and not full: bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    if planar_uv:  # re-project after merge
        uvl = me.uv_layers[0]
        for poly in me.polygons:
            for li in poly.loop_indices:
                co = me.vertices[me.loops[li].vertex_index].co
                uvl.data[li].uv = (0.5 + co.x / (2 * planar_uv), 0.5 + co.y / (2 * planar_uv))
    for poly in me.polygons: poly.use_smooth = True
    if parent: ob.parent = parent
    return ob

def arc(cx, cz, rad, a_from, a_to, steps):
    return [(cx + rad * math.cos(math.radians(a_from + (a_to - a_from) * k / steps)),
             cz + rad * math.sin(math.radians(a_from + (a_to - a_from) * k / steps))) for k in range(steps + 1)]

root = bpy.data.objects.new("LexioTable", None); scene.collection.objects.link(root)

TOP = 0.755
# Felt play surface (planar UV covering radius 0.93)
lathe("Felt", [(0.0, TOP), (0.31, TOP), (0.62, TOP), (0.93, TOP), (0.93, TOP - 0.012)], M_FELT, planar_uv=0.93, parent=root)
# Cream cushion band around the felt
lathe("Rim_CreamBand", [(0.924, 0.750), (0.932, 0.763), (0.945, 0.771), (0.965, 0.775), (0.985, 0.775), (0.998, 0.772), (1.002, 0.769)], M_CREAM, parent=root)
# Blue panel band
lathe("Rim_BlueBand", [(1.0, 0.769), (1.025, 0.770), (1.052, 0.771)], M_BLUE, parent=root)
# Raised yellow outer lip
lathe("Rim_YellowLip", [(1.048, 0.770)] + arc(1.082, 0.760, 0.030, 95, -15, 12) + [(1.112, 0.748)], M_YELLOW, parent=root)
# Blue side wall, curving under the apron toward the pedestal
side = [(1.110, 0.750), (1.116, 0.735), (1.118, 0.70), (1.116, 0.668)] + arc(1.088, 0.668, 0.028, -10, -95, 8)[1:] \
       + [(0.95, 0.630), (0.75, 0.622), (0.60, 0.618)]
lathe("Rim_SideApron", side, M_BLUE, parent=root)
# Pedestal
col = [(0.60, 0.618), (0.56, 0.605), (0.50, 0.585), (0.44, 0.555), (0.39, 0.51), (0.35, 0.45),
       (0.325, 0.39), (0.312, 0.33), (0.308, 0.27)]
lathe("Pedestal_Column", col, M_BLUE, parent=root)
lathe("Pedestal_WaistRing", [(0.309, 0.272), (0.326, 0.262), (0.326, 0.226), (0.309, 0.216)], M_YELLOW, parent=root)
lathe("Pedestal_Collar", [(0.308, 0.216), (0.33, 0.205), (0.39, 0.180), (0.47, 0.162), (0.52, 0.156)], M_BLUE, parent=root)
lathe("Base_TopTrim", [(0.518, 0.157)] + arc(0.53, 0.146, 0.016, 100, -20, 8) + [(0.546, 0.135)], M_CREAM, parent=root)
lathe("Base_Side", [(0.546, 0.136), (0.556, 0.12), (0.560, 0.07), (0.562, 0.058)], M_BLUE, parent=root)
lathe("Base_BottomTrim", [(0.560, 0.060)] + arc(0.568, 0.04, 0.02, 90, -60, 8) + [(0.575, 0.0)], M_YELLOW, parent=root)
lathe("Base_Bottom", [(0.578, 0.005), (0.30, 0.002), (0.0, 0.002)], M_BLUE, planar_uv=0.6, parent=root)

# Smooth-by-angle so hard creases stay crisp
for ob in root.children:
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    try: bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    except Exception as e: print("smooth:", e)
    ob.select_set(False)

# ---------------------------------------------------------------- export
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "LexioTable.fbx"), use_selection=True,
                         path_mode="COPY", embed_textures=True, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                         object_types={"EMPTY", "MESH"}, mesh_smooth_type="FACE")
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, "LexioTable.glb"), export_format="GLB", use_selection=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "LexioTable.blend"))

# ---------------------------------------------------------------- preview render
if "--render" in sys.argv:
    w = bpy.data.worlds.new("W"); scene.world = w
    try: w.use_nodes = True
    except Exception: pass
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.5, 0.55, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.35
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, -0.001))
    fl = bpy.context.object; fm, fb, _ = principled("Floor"); fl.data.materials.append(fm)
    fb.inputs["Base Color"].default_value = srgb(64, 46, 160); fb.inputs["Roughness"].default_value = 0.9
    def light(name, loc, energy, size, color=(1, 1, 1)):
        d = bpy.data.lights.new(name, "AREA"); d.energy = energy; d.size = size; d.color = color
        o = bpy.data.objects.new(name, d); scene.collection.objects.link(o); o.location = loc
        o.rotation_euler = (Vector((0, 0, 0.4)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    light("Top", (0, -0.8, 4.5), 170, 5)
    light("Front", (0, -4.0, 3.0), 60, 4)
    cd = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cd); scene.collection.objects.link(cam)
    scene.camera = cam
    sc = scene.cycles; scene.render.engine = "CYCLES"; sc.device = "CPU"; sc.samples = int(os.environ.get("SPP", 48))
    sc.use_denoising = True
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = 1200, 760
    views = {"hero": ((0.0, -3.5, 2.05), (0, 0, 0.42), 45),
             "game": ((0.0, -1.75, 2.75), (0, -0.1, 0.75), 24),
             "front": ((0.0, -4.2, 0.55), (0, 0, 0.40), 45),
             "top": ((0.0, -0.001, 5.2), (0, 0, 0.4), 40),
             "detail": ((0.55, -1.55, 1.00), (0.38, -0.98, 0.70), 50)}
    for key in os.environ.get("VIEWS", "hero,front,top,detail").split(","):
        loc, tgt, f = views[key]
        cam.location = loc; cd.lens = f
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(OUT, f"preview_{key}.png")
        bpy.ops.render.render(write_still=True)
print("DONE")
