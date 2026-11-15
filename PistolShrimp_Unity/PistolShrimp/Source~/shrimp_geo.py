"""Low-poly pistol shrimp geometry, authored in Unity-style axes.

Authoring space: R = right (+X in Unity), U = up (+Y), F = forward (+Z). Metres.
The shrimp is authored at "player scale" (about 1.3 m long) so the claws are
hand-sized in VR.

build(side) returns (parts, bones, markers)
  side = +1 -> snapper claw on the right, -1 -> on the left.
"""
from math import cos, sin, pi
import numpy as np


def V(r, u, f):
    return np.array([r, u, f], dtype=float)


def norm(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v


# ---------------------------------------------------------------- primitives
def ring(center, hw, hh, n=8, rot=0.0, right=None, up=None, bottom=1.0, top=1.0):
    right = V(1, 0, 0) if right is None else right
    up = V(0, 1, 0) if up is None else up
    pts = []
    for k in range(n):
        t = 2 * pi * (k + rot) / n
        c, s = cos(t), sin(t)
        y = hh * s * (top if s > 0 else bottom)
        pts.append(center + right * hw * c + up * y)
    return pts


def loft(rings, roles, tips=(None, None), cap_roles=(None, None), role_fn=None):
    """rings: list of equal-length point lists. roles: one role per band."""
    n = len(rings[0])
    verts = [p for r in rings for p in r]
    faces, froles = [], []
    for b in range(len(rings) - 1):
        for k in range(n):
            a, a2 = b * n + k, b * n + (k + 1) % n
            c, c2 = (b + 1) * n + k, (b + 1) * n + (k + 1) % n
            faces.append((a, a2, c2, c))
            froles.append(role_fn(b, k) if role_fn else roles[b])
    last = (len(rings) - 1) * n
    r0 = cap_roles[0] or roles[0]
    r1 = cap_roles[1] or roles[-1]
    if tips[0] is not None:
        i = len(verts); verts.append(tips[0])
        for k in range(n):
            faces.append((i, (k + 1) % n, k)); froles.append(r0)
    else:
        faces.append(tuple(reversed(range(n)))); froles.append(r0)
    if tips[1] is not None:
        i = len(verts); verts.append(tips[1])
        for k in range(n):
            faces.append((i, last + k, last + (k + 1) % n)); froles.append(r1)
    else:
        faces.append(tuple(last + k for k in range(n))); froles.append(r1)
    return verts, faces, froles


def tube(points, radii, roles, n=5, up_hint=None, rot=0.25, squash=1.0, tips=(None, None)):
    up_hint = V(0, 1, 0) if up_hint is None else up_hint
    rings = []
    for i, p in enumerate(points):
        a = points[max(i - 1, 0)]
        b = points[min(i + 1, len(points) - 1)]
        t = norm(b - a)
        right = np.cross(up_hint, t)
        if np.linalg.norm(right) < 1e-4:
            right = np.cross(V(1, 0, 0), t)
        right = norm(right)
        up = norm(np.cross(t, right))
        rings.append(ring(p, radii[i], radii[i] * squash, n, rot, right, up))
    return loft(rings, roles, tips=tips)


def plate(outline, thickness, role, normal):
    """Thin closed solid from a planar outline (list of points)."""
    n = len(outline)
    off = norm(normal) * thickness * 0.5
    verts = [p + off for p in outline] + [p - off for p in outline]
    faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))]
    for k in range(n):
        k2 = (k + 1) % n
        faces.append((k, k2, n + k2, n + k))
    return verts, faces, [role] * len(faces)


def octa(center, rx, ry, rz, role):
    c = center
    verts = [c + V(rx, 0, 0), c + V(-rx, 0, 0), c + V(0, ry, 0),
             c + V(0, -ry, 0), c + V(0, 0, rz), c + V(0, 0, -rz)]
    faces = [(0, 2, 4), (2, 1, 4), (1, 3, 4), (3, 0, 4),
             (2, 0, 5), (1, 2, 5), (3, 1, 5), (0, 3, 5)]
    return verts, faces, [role] * 8


# ---------------------------------------------------------------- the shrimp
def build(side=1):
    parts = []   # dict(name, bone, verts, faces, roles)
    bones = []   # dict(name, head, tail, parent)

    def add(name, bone, geo):
        v, f, r = geo
        parts.append(dict(name=name, bone=bone, verts=[np.array(p, float) for p in v],
                          faces=f, roles=r))

    def bone(name, head, tail, parent=None):
        bones.append(dict(name=name, head=np.array(head, float),
                          tail=np.array(tail, float), parent=parent))

    def mir(p, s):
        return V(p[0] * s, p[1], p[2])

    # ---- carapace ---------------------------------------------------------
    N = 8
    car = [  # F, half-width, half-height, U-centre
        (0.300, 0.062, 0.060, 0.022),
        (0.215, 0.112, 0.108, 0.004),
        (0.070, 0.135, 0.128, 0.000),
        (-0.070, 0.130, 0.122, 0.000),
        (-0.165, 0.108, 0.102, 0.004),
    ]
    rings = [ring(V(0, uc, f), hw, hh, N, rot=0.5, bottom=0.78) for f, hw, hh, uc in car]

    def car_role(b, k):
        # ring index 0 sits at angle rot/N; faces k=4,5 (of 8) are the belly
        mid = 2 * pi * (k + 1.0) / N
        if sin(mid) < -0.6:
            return 'belly'
        return ['carapace_1', 'carapace_2', 'carapace_3', 'carapace_4'][b]

    add('carapace', 'Carapace',
        loft(rings, ['carapace_1'] * 4, tips=(V(0, 0.052, 0.415), None),
             cap_roles=('rostrum', 'carapace_4'), role_fn=car_role))
    bone('Carapace', V(0, 0, -0.165), V(0, 0, 0.30))

    # eyes: chunky dark gems on short stalks
    for s, tag in ((1, 'R'), (-1, 'L')):
        add('eye_' + tag, 'Carapace', octa(V(0.058 * s, 0.085, 0.292), 0.036, 0.038, 0.040, 'eye'))

    # ---- abdomen: overlapping armour plates --------------------------------
    abd = [  # joint F, U-centre, front hw/hh, back hw/hh
        (-0.165, 0.004),
        (-0.285, -0.004),
        (-0.400, -0.022),
        (-0.510, -0.050),
        (-0.612, -0.088),
        (-0.705, -0.132),
    ]
    sizes = [(0.118, 0.112, 0.104, 0.098), (0.110, 0.104, 0.094, 0.088),
             (0.100, 0.094, 0.082, 0.076), (0.088, 0.082, 0.068, 0.062),
             (0.074, 0.068, 0.052, 0.044)]
    M = 6
    parent = 'Carapace'
    for i in range(5):
        f0, u0 = abd[i]; f1, u1 = abd[i + 1]
        hw0, hh0, hw1, hh1 = sizes[i]
        fwd = norm(V(0, u1 - u0, f1 - f0))
        up = norm(np.cross(fwd, V(1, 0, 0))) * -1.0
        if up[1] < 0: up = -up
        ra = ring(V(0, u0, f0 + 0.012), hw0, hh0, M, rot=0.0, up=up, bottom=0.8)
        rb = ring(V(0, u1, f1), hw1, hh1, M, rot=0.0, up=up, bottom=0.8)
        role = 'abd_%d' % (i + 1)

        def abd_role(b, k, role=role):
            return 'belly' if k in (3, 4) and False else role
        geo = loft([ra, rb], [role], role_fn=abd_role)
        # belly: faces whose ring edge is the bottom edge (k = 4 for 6-gon rot 0)
        v, f, r = geo
        r = list(r)
        r[4] = 'belly'
        add('abdomen_%d' % (i + 1), 'Abd%d' % (i + 1), (v, f, r))
        bone('Abd%d' % (i + 1), V(0, u0, f0), V(0, u1, f1), parent)
        parent = 'Abd%d' % (i + 1)

    # eye-spot on the side of segment 4 (tiger variant), both sides
    f0, u0 = abd[3]; f1, u1 = abd[4]
    for s in (1, -1):
        c = V(0.080 * s, (u0 + u1) / 2 + 0.012, (f0 + f1) / 2 + 0.006)
        nrm = norm(V(1.0 * s, 0.25, -0.18))
        t1 = norm(np.cross(nrm, V(0, 1, 0))); t2 = norm(np.cross(nrm, t1))
        hexo = [c + (t1 * cos(a) + t2 * sin(a)) * 0.036 for a in np.linspace(0, 2 * pi, 6, endpoint=False)]
        hexi = [c + nrm * 0.006 + (t1 * cos(a) + t2 * sin(a)) * 0.020 for a in np.linspace(0, 2 * pi, 6, endpoint=False)]
        add('spot_ring', 'Abd4', plate(hexo, 0.012, 'spot_ring', nrm))
        add('spot_center', 'Abd4', plate(hexi, 0.012, 'spot_center', nrm))

    # ---- tail fan ----------------------------------------------------------
    tf, tu = abd[5]
    tdir = norm(V(0, -0.30, -1.0))
    tn = norm(np.cross(V(1, 0, 0), tdir))
    if tn[1] < 0: tn = -tn
    base = V(0, tu, tf + 0.010)

    def fan(angle_deg, length, width, root_w):
        a = np.radians(angle_deg)
        d = norm(tdir * cos(a) + V(1, 0, 0) * sin(a))
        sidev = norm(np.cross(tn, d))
        return [base - sidev * root_w, base + d * length * 0.55 - sidev * width,
                base + d * length, base + d * length * 0.55 + sidev * width,
                base + sidev * root_w]
    add('telson', 'Tail', plate(fan(0, 0.175, 0.040, 0.030), 0.022, 'tail_center', tn))
    for ang in (-34, 34):
        add('uropod_in', 'Tail', plate(fan(ang, 0.170, 0.046, 0.022), 0.016, 'tail_fan', tn))
    for ang in (-66, 66):
        add('uropod_out', 'Tail', plate(fan(ang, 0.140, 0.040, 0.018), 0.014, 'tail_fan', tn))
    bone('Tail', V(0, tu, tf), V(0, tu, tf) + tdir * 0.17, 'Abd5')

    # ---- pleopods (swimmerets) --------------------------------------------
    for i in range(4):
        f0, u0 = abd[i]; f1, u1 = abd[i + 1]
        hw = sizes[i][0]
        for s, tag in ((1, 'R'), (-1, 'L')):
            root = V(0.048 * s, (u0 + u1) / 2 - sizes[i][1] * 0.70, (f0 + f1) / 2 + 0.01)
            d = norm(V(0.10 * s, -1.0, -0.45))
            L = 0.105 - i * 0.008
            w = V(0, 0, 1) * 0.026
            outline = [root - w * 0.6, root + d * L * 0.6 - w, root + d * L,
                       root + d * L * 0.6 + w, root + w * 0.6]
            name = 'Pleo_%s%d' % (tag, i + 1)
            add(name, name, plate(outline, 0.014, 'pleopod', V(1, 0, 0)))
            bone(name, root, root + d * L, 'Abd%d' % (i + 1))

    # ---- walking legs (4 pairs) --------------------------------------------
    leg_f = [0.135, 0.050, -0.035, -0.115]
    splay = [0.060, 0.020, -0.030, -0.075]
    for i in range(4):
        for s, tag in ((1, 'R'), (-1, 'L')):
            hip = V(0.085 * s, -0.082, leg_f[i])
            knee = V(0.215 * s, -0.150 + i * 0.004, leg_f[i] + splay[i])
            foot = V(0.262 * s, -0.345, leg_f[i] + splay[i] * 2.2)
            nu = 'Leg_%s%d_Upper' % (tag, i + 1); nl = 'Leg_%s%d_Lower' % (tag, i + 1)
            add(nu, nu, tube([hip, knee], [0.024, 0.017], ['leg_upper'], n=4))
            add(nl, nl, tube([knee, (knee * 0.35 + foot * 0.65)], [0.018, 0.012], ['leg_lower'], n=4,
                             up_hint=V(0, 0, 1), tips=(None, foot)))
            bone(nu, hip, knee, 'Carapace')
            bone(nl, knee, foot, nu)

    # ---- antennae ----------------------------------------------------------
    for s, tag in ((1, 'R'), (-1, 'L')):
        p = [V(0.072 * s, -0.012, 0.292), V(0.215 * s, 0.020, 0.500),
             V(0.400 * s, 0.070, 0.670), V(0.610 * s, 0.105, 0.780)]
        rad = [0.013, 0.010, 0.007, 0.003]
        par = 'Carapace'
        for j in range(3):
            name = 'Antenna_%s_%d' % (tag, j + 1)
            tip = p[3] if j == 2 else None
            end = (p[j] * 0.15 + p[j + 1] * 0.85) if j == 2 else p[j + 1]
            add(name, name, tube([p[j], end], [rad[j], rad[j + 1] if j < 2 else 0.0055],
                                 ['antenna'], n=3, tips=(None, tip)))
            bone(name, p[j], p[j + 1], par)
            par = name
        # short forked antennule, kept low so it frames the view instead of blocking it
        a0 = V(0.026 * s, 0.018, 0.325)
        a1 = V(0.085 * s, 0.030, 0.520)
        name = 'Antennule_' + tag
        add(name, name, tube([a0, a0 * 0.2 + a1 * 0.8], [0.010, 0.005], ['antenna'], n=3, tips=(None, a1)))
        a2 = V(0.030 * s, 0.062, 0.500)
        mid = a0 * 0.55 + a1 * 0.45
        add(name + '_b', name, tube([mid, mid * 0.25 + a2 * 0.75], [0.007, 0.004], ['antenna'], n=3, tips=(None, a2)))
        bone(name, a0, a1, 'Carapace')

    # ---- claw arms ---------------------------------------------------------
    def arm(prefix, s, shoulder, elbow, wrist, r_up, r_lo):
        add(prefix + '_ArmUpper', prefix + '_ArmUpper',
            tube([shoulder, shoulder * 0.45 + elbow * 0.55, elbow],
                 [r_up * 0.80, r_up * 1.15, r_up * 0.95], ['arm_upper'] * 2, n=5))
        add(prefix + '_ArmLower', prefix + '_ArmLower',
            tube([elbow, elbow * 0.45 + wrist * 0.55, wrist],
                 [r_lo * 0.85, r_lo * 1.20, r_lo * 0.85], ['arm_lower'] * 2, n=5))
        bone(prefix + '_ArmUpper', shoulder, elbow, 'Carapace')
        bone(prefix + '_ArmLower', elbow, wrist, prefix + '_ArmUpper')

    # snapper side
    s = side
    sh = V(0.100 * s, -0.070, 0.225)
    el = V(0.300 * s, -0.150, 0.520)
    wr = V(0.250 * s, -0.045, 0.860)
    arm('Snap', s, sh, el, wr, 0.040, 0.044)

    def claw_pt(r, u, f, origin, sgn):
        return origin + V(r * sgn, u, f)

    # snapper claw: big bulbous palm, short fixed finger, hooked hammer on top
    K = 8
    palm = [  # F, hw, hh, uc
        (0.000, 0.040, 0.040, 0.000),
        (0.060, 0.088, 0.078, 0.004),
        (0.165, 0.104, 0.092, 0.006),
        (0.265, 0.092, 0.082, 0.004),
        (0.325, 0.070, 0.058, -0.010),
    ]
    finger = [(0.385, 0.048, 0.030, -0.038), (0.440, 0.028, 0.017, -0.036)]
    rings = [ring(claw_pt(0, uc, f, wr, s), hw, hh, K, rot=0.5, bottom=0.85)
             for f, hw, hh, uc in palm + finger]
    add('snap_claw', 'Snap_Claw',
        loft(rings, ['snap_palm_a', 'snap_palm_b', 'snap_palm_c', 'snap_palm_c', 'snap_finger', 'snap_finger'],
             tips=(None, claw_pt(0, -0.022, 0.485, wr, s))))
    dact = [(0.305, 0.050, 0.030, 0.052), (0.365, 0.047, 0.036, 0.044), (0.430, 0.030, 0.026, 0.020)]
    rings = [ring(claw_pt(0, uc, f, wr, s), hw, hh, 6, rot=0.0, bottom=0.6)
             for f, hw, hh, uc in dact]
    add('snap_dactyl', 'Snap_Dactyl',
        loft(rings, ['snap_dactyl'] * 2, tips=(None, claw_pt(0, -0.012, 0.488, wr, s))))
    hinge = claw_pt(0, 0.040, 0.300, wr, s)
    muzzle = claw_pt(0, -0.004, 0.500, wr, s)
    bone('Snap_Claw', wr, claw_pt(0, 0, 0.30, wr, s), 'Snap_ArmLower')
    bone('Snap_Dactyl', hinge, claw_pt(0, 0.0, 0.485, wr, s), 'Snap_Claw')
    bone('Snap_Muzzle', muzzle, muzzle + V(0, 0, 0.05), 'Snap_Claw')

    # pincer side
    s = -side
    sh = V(0.100 * s, -0.070, 0.225)
    el = V(0.285 * s, -0.150, 0.520)
    wr = V(0.235 * s, -0.060, 0.860)
    arm('Pinch', s, sh, el, wr, 0.032, 0.032)
    palm = [(0.000, 0.026, 0.026, 0.0), (0.045, 0.050, 0.044, 0.0),
            (0.120, 0.052, 0.046, 0.0), (0.170, 0.040, 0.034, -0.006)]
    finger = [(0.225, 0.024, 0.015, -0.022), (0.275, 0.015, 0.010, -0.020)]
    rings = [ring(claw_pt(0, uc, f, wr, s), hw, hh, 6, rot=0.0, bottom=0.85)
             for f, hw, hh, uc in palm + finger]
    add('pinch_claw', 'Pinch_Claw',
        loft(rings, ['pinch_palm', 'pinch_palm', 'pinch_palm', 'pinch_finger', 'pinch_finger'],
             tips=(None, claw_pt(0, -0.010, 0.315, wr, s))))
    dact = [(0.165, 0.028, 0.015, 0.026), (0.225, 0.022, 0.014, 0.014), (0.275, 0.014, 0.010, 0.004)]
    rings = [ring(claw_pt(0, uc, f, wr, s), hw, hh, 4, rot=0.5, bottom=0.6)
             for f, hw, hh, uc in dact]
    add('pinch_dactyl', 'Pinch_Dactyl',
        loft(rings, ['pinch_finger'] * 2, tips=(None, claw_pt(0, -0.004, 0.317, wr, s))))
    hinge = claw_pt(0, 0.020, 0.160, wr, s)
    tipm = claw_pt(0, -0.006, 0.300, wr, s)
    bone('Pinch_Claw', wr, claw_pt(0, 0, 0.17, wr, s), 'Pinch_ArmLower')
    bone('Pinch_Dactyl', hinge, claw_pt(0, 0.0, 0.315, wr, s), 'Pinch_Claw')
    bone('Pinch_Tip', tipm, tipm + V(0, 0, 0.05), 'Pinch_Claw')

    # ---- markers -----------------------------------------------------------
    bone('EyePoint', V(0, 0.150, 0.300), V(0, 0.150, 0.350), 'Carapace')
    return parts, bones


ROLES = ['carapace_1', 'carapace_2', 'carapace_3', 'carapace_4', 'belly', 'rostrum',
         'abd_1', 'abd_2', 'abd_3', 'abd_4', 'abd_5', 'tail_center', 'tail_fan', 'eye',
         'arm_upper', 'arm_lower', 'snap_palm_a', 'snap_palm_b', 'snap_palm_c',
         'snap_finger', 'snap_dactyl', 'pinch_palm', 'pinch_finger', 'leg_upper',
         'leg_lower', 'antenna', 'pleopod', 'spot_ring', 'spot_center']

WHITE, RED = 'F4ECE2', 'D42A20'
PALETTES = {
    'CandyStripe': dict(
        carapace_1=WHITE, carapace_2=RED, carapace_3=WHITE, carapace_4=RED, belly='F1DCCB',
        rostrum=RED, abd_1=WHITE, abd_2=RED, abd_3=WHITE, abd_4=RED, abd_5=WHITE,
        tail_center=RED, tail_fan='F6D8CC', eye='17171F', arm_upper=WHITE, arm_lower='E4532A',
        snap_palm_a='E4532A', snap_palm_b=WHITE, snap_palm_c=RED, snap_finger=WHITE,
        snap_dactyl='E4532A', pinch_palm=WHITE, pinch_finger=RED, leg_upper='E2D23C',
        leg_lower='C6DB45', antenna='DB4A30', pleopod='F2C8B6', spot_ring=RED, spot_center=RED),
    'TigerOrange': dict(
        carapace_1='F09A3C', carapace_2='E8842A', carapace_3='EF9436', carapace_4='E27C26',
        belly='F4BE7C', rostrum='E27C26', abd_1='EC8C30', abd_2='E27C26', abd_3='EC8C30',
        abd_4='E27C26', abd_5='EC8C30', tail_center='E27C26', tail_fan='F2A856', eye='24402E',
        arm_upper='D9C2E6', arm_lower='B79AD6', snap_palm_a='8C68BE', snap_palm_b='7C58B2',
        snap_palm_c='9A78C8', snap_finger='E0A070', snap_dactyl='E8AE80', pinch_palm='8C68BE',
        pinch_finger='E0A070', leg_upper='F0CDB6', leg_lower='E8BBA0', antenna='E88A3A',
        pleopod='F2B26C', spot_ring='F6EEE4', spot_center='5C1422'),
    'GlassRedBand': dict(
        carapace_1='D8483C', carapace_2='EFE7E8', carapace_3='E8E0E6', carapace_4='EFE7E8',
        belly='F6F0EE', rostrum='C83028', abd_1='C8262C', abd_2='E6DCE4', abd_3='EDE5EA',
        abd_4='E2D8E2', abd_5='EDE5EA', tail_center='DCD2E0', tail_fan='E8E0EC', eye='1C1C24',
        arm_upper='EFD9C8', arm_lower='E6C4A6', snap_palm_a='E8C69A', snap_palm_b='EFD3AC',
        snap_palm_c='E8C69A', snap_finger='E9A292', snap_dactyl='F2E2CC', pinch_palm='DDA878',
        pinch_finger='E9A292', leg_upper='CCD6E6', leg_lower='B8C6DC', antenna='DDB6AE',
        pleopod='E2DAE8', spot_ring='E2D8E2', spot_center='E2D8E2'),
}
