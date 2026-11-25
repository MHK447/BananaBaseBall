using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The arena changes with the map of the journey: the Royal Sandpit, the Tidal Bastion out in its harbour, the
    /// Ember Crown over its lava, the Frostspire, the Pharaoh Sands of the desert, the Jade Temple in its jungle and
    /// the Gridiron Bowl. The court, the walls and the stands stand where they always do; what they are made of, and
    /// what is painted and built on them, is the look.
    /// </summary>
    public sealed partial class ColosseumArena
    {
        /// <summary>What one map's arena is made of: its colours, and by its number which ornaments it gets.</summary>
        internal sealed class Look
        {
            public const int Sandpit = 0, Tidal = 1, Ember = 2, Frost = 3, Pharaoh = 4, Jade = 5, Gridiron = 6;

            public int Index;
            /// <summary>What the camera clears to behind everything.</summary>
            public Color32 Sky;
            // The ground: what lies far outside, the strip the walls stand in, and the tiles of the court.
            public Color32 Void, Apron, Grout, TileLight, TileDark, TileWorn, WashBlue, WashRed;
            public float Wash = .10f, ShadeLow = .975f, ShadeHigh = 1.02f;
            public Color32 Line, LineLight, LineDeep, Seal, Stain, StainEdge, PebbleA, PebbleB, Shade;
            public int Stains = 22;
            // The walls, their towers and what stands on them.
            public Color32 Stone, StoneTop, StoneShade, Plinth, PlinthTop, Cap, Block, BlockDark, Trim, Bowl, Pole;
            // The stands (seat, riser, lip) for the far and the near half, their stairs, and the arcade behind.
            public Color32[] StandRed, StandBlue;
            public Color32 Stair, StairEdge, Arcade, ArcadeTop, Arch, Column, Cornice, CorniceTop, Capital;
            // The royal box and the gates.
            public Color32 BoxStone, BoxTop, Drape, DrapeDeep, Curtain, CurtainDeep, StripeA, StripeATop, StripeB, StripeBTop, Iron, GateDark;
            // What an earthquake lays bare: the face of the ditch from the top down, its bright lip, and the cracks.
            public Color32[] Pit;
            public Color32 Lip, CrackEdge, CrackDark;
            // The dent a heavy landing leaves in the court: its soot, its bright rim, its pit, its cracks and the clods round it.
            public Color32 Soot, CraterRim, CraterDeep, CraterMid, CraterCrack, Clod;

            static Color32 A(int r, int g, int b, int a = 255) { return new Color32((byte)r, (byte)g, (byte)b, (byte)a); }

            static readonly Look[] All = { RoyalSandpit(), TidalBastion(), EmberCrown(), Frostspire(), PharaohSands(), JadeTemple(), GridironBowl() };

            /// <summary>The look of the given arena of the journey: they come round again in the order of its maps.</summary>
            public static Look For(int stageNumber) { return All[(Mathf.Max(1, stageNumber) - 1) % All.Length]; }

            static Look RoyalSandpit()
            {
                return new Look
                {
                    Index = Sandpit, Sky = A(46, 34, 48),
                    Void = C(0x2e2230), Apron = C(0x6e5244), Grout = C(0xc9ab78), TileLight = C(0xefdcb2), TileDark = C(0xe4cc9c), TileWorn = C(0xd9c090),
                    WashBlue = C(0xcfe0ff), WashRed = C(0xffd3c4),
                    Line = C(0xb8925a), LineLight = C(0xfbeecb), LineDeep = C(0x9c7a48), Seal = C(0xe9d3a4),
                    Stain = C(0xdcc594), StainEdge = C(0xe4cf9f), PebbleA = C(0xd9c49a), PebbleB = C(0xc9b288), Shade = A(74, 40, 30, 96),
                    Stone = C(0xc4a67c, .12f), StoneTop = C(0xe9dcc0, .15f), StoneShade = C(0xa98c64, .1f), Plinth = C(0x7c5c44, .05f), PlinthTop = C(0x9a7a5c, .05f),
                    Cap = C(0xfbf3df, .2f), Block = C(0xddcdaa, .2f), BlockDark = C(0xc2ad88, .2f), Trim = C(Kit.Gold, .9f), Bowl = C(0x4b3a44, .5f), Pole = C(0x6f4529, .1f),
                    StandRed = new[] { C(0x7a4846), C(0x5a3238), C(0x9c6a5e) }, StandBlue = new[] { C(0x475889), C(0x333f6b), C(0x7285b3) },
                    Stair = C(0xd9c19a), StairEdge = C(0xb39870), Arcade = C(0xc79f74), ArcadeTop = C(0xe8cfa2), Arch = C(0x3a2630),
                    Column = C(0xecd9b4), Cornice = C(0xecd9b4), CorniceTop = C(0xfff3d6), Capital = C(0xfff3d6),
                    BoxStone = C(0xd9c7a2, .1f), BoxTop = C(0xf3e8cc, .1f), Drape = C(0x8d52da, .2f), DrapeDeep = C(0x6a35b4, .2f),
                    Curtain = C(0x7a3ccf, .25f), CurtainDeep = C(0x57259c, .25f),
                    StripeA = C(0x6a35b4, .2f), StripeATop = C(0x8d52da, .2f), StripeB = C(Kit.Gold, .9f), StripeBTop = C(0xffd76a, .6f),
                    Iron = C(0x70758a, .7f), GateDark = C(0x33252c),
                    Pit = new[] { A(150, 108, 78), A(114, 80, 62), A(84, 58, 50), A(104, 72, 58), A(70, 48, 46), A(52, 36, 40), A(60, 41, 43), A(28, 19, 33) },
                    Lip = A(252, 238, 200, 235), CrackEdge = A(255, 242, 208, 170), CrackDark = A(58, 36, 28, 235),
                    Soot = A(70, 42, 26, 150), CraterRim = A(250, 226, 170, 210), CraterDeep = A(44, 26, 18, 235), CraterMid = A(96, 60, 36, 225),
                    CraterCrack = A(48, 28, 18, 220), Clod = A(122, 78, 46, 235)
                };
            }

            // A sea fort: sea-green flagstones round a golden anchor, pale harbour stone with gilded bollards, a
            // moat of harbour water, and a striped lighthouse on every corner.
            static Look TidalBastion()
            {
                return new Look
                {
                    Index = Tidal, Sky = A(10, 70, 112),
                    Void = C(0x12648f), Apron = C(0x1f93cc), Grout = C(0x2596a0), TileLight = C(0x4fd2c9), TileDark = C(0x3cc0be), TileWorn = C(0x73dfd0),
                    WashBlue = C(0x9fd0ff), WashRed = C(0xffe2c4), Wash = .07f,
                    Line = C(0x1f8d98), LineLight = C(0xfbeecb), LineDeep = C(0x17717e), Seal = C(0x2a9da4),
                    Stain = C(0x5ed6cb), StainEdge = C(0x49c9c3), PebbleA = C(0xf6e8c8), PebbleB = C(0xe2cda4), Shade = A(4, 62, 86, 104), Stains = 12,
                    Stone = C(0xe9d6b0, .12f), StoneTop = C(0xfaf0da, .15f), StoneShade = C(0xc7ae86, .1f), Plinth = C(0x6d8fa3, .1f), PlinthTop = C(0x9dbccb, .1f),
                    Cap = C(0xf7c545, .7f), Block = C(0xfaf0da, .2f), BlockDark = C(0xe6d2aa, .2f), Trim = C(Kit.Gold, .9f), Bowl = C(0x2b5fc0, .5f), Pole = C(0x9a6436, .1f),
                    StandRed = new[] { C(0xb9523f), C(0x8c3a30), C(0xdf7a60) }, StandBlue = new[] { C(0x2f6fae), C(0x22528a), C(0x5d9bd4) },
                    Stair = C(0xf3e6c8), StairEdge = C(0xd3bf98), Arcade = C(0xe9d6b0), ArcadeTop = C(0xfaf0da), Arch = C(0x14506a),
                    Column = C(0xfaf0da), Cornice = C(0xc9432f), CorniceTop = C(0xf26449), Capital = C(Kit.Gold),
                    BoxStone = C(0xa9683a, .1f), BoxTop = C(0xcf9558, .1f), Drape = C(0x2f73e0, .2f), DrapeDeep = C(0x1f52b0, .2f),
                    Curtain = C(0x2f73e0, .25f), CurtainDeep = C(0x1f52b0, .25f),
                    StripeA = C(0xc9402e, .2f), StripeATop = C(0xf05a44, .2f), StripeB = C(0xe8decb, .2f), StripeBTop = C(0xfff7e6, .2f),
                    Iron = C(Kit.Gold, .9f), GateDark = C(0x1c57b8),
                    Pit = new[] { A(70, 176, 218), A(42, 144, 194), A(26, 114, 168), A(34, 128, 180), A(18, 94, 148), A(10, 70, 122), A(12, 80, 132), A(6, 50, 98) },
                    Lip = A(236, 252, 250, 235), CrackEdge = A(226, 252, 246, 180), CrackDark = A(8, 62, 98, 235),
                    Soot = A(6, 60, 88, 150), CraterRim = A(214, 250, 244, 215), CraterDeep = A(8, 62, 96, 235), CraterMid = A(24, 110, 146, 225),
                    CraterCrack = A(8, 60, 92, 220), Clod = A(120, 226, 212, 235)
                };
            }

            // A crown of dark stone over fire: slate flags split by glowing seams round a golden flame, gilded
            // spikes along the walls, a moat of lava, and a burning crown on every corner.
            static Look EmberCrown()
            {
                return new Look
                {
                    Index = Ember, Sky = A(24, 14, 26),
                    Void = C(0x1c1220), Apron = C(0xff8a1c), Grout = C(0x3d3149), TileLight = C(0x746384), TileDark = C(0x67577a), TileWorn = C(0x5a4b69),
                    WashBlue = C(0x8fb0ff), WashRed = C(0xff9a86), Wash = .09f, ShadeLow = .95f, ShadeHigh = 1.04f,
                    Line = C(0xc08a2e), LineLight = C(0xffd76a), LineDeep = C(0x8a5f1c), Seal = C(0x463a52),
                    Stain = C(0x62536f), StainEdge = C(0x6d5d7c), PebbleA = C(0x3c3046), PebbleB = C(0x8a4a2a), Shade = A(255, 116, 26, 78), Stains = 14,
                    Stone = C(0x5c4f6a, .12f), StoneTop = C(0x7d6e90, .15f), StoneShade = C(0x44394f, .1f), Plinth = C(0x2c2234, .05f), PlinthTop = C(0x483b54, .05f),
                    Cap = C(0xf2b53a, .8f), Block = C(0x71627f, .2f), BlockDark = C(0x574a66, .2f), Trim = C(Kit.Gold, .9f), Bowl = C(0x2f2536, .5f), Pole = C(0x3a2e40, .1f),
                    StandRed = new[] { C(0x66303c), C(0x47222c), C(0x94495a) }, StandBlue = new[] { C(0x3c426c), C(0x2a2e50), C(0x5d669c) },
                    Stair = C(0x8d7d9c), StairEdge = C(0x6a5c78), Arcade = C(0x4a3d56), ArcadeTop = C(0x6a5c78), Arch = C(0xff7a1c),
                    Column = C(0x71627f), Cornice = C(0xd48a1c), CorniceTop = C(0xf7b836), Capital = C(Kit.Gold),
                    BoxStone = C(0x5c4f6a, .1f), BoxTop = C(0x7d6e90, .1f), Drape = C(0xc8272f, .2f), DrapeDeep = C(0x8e1a25, .2f),
                    Curtain = C(0xc8272f, .25f), CurtainDeep = C(0x8e1a25, .25f),
                    StripeA = C(0x8e1a25, .2f), StripeATop = C(0xc8272f, .2f), StripeB = C(Kit.Gold, .9f), StripeBTop = C(0xffd76a, .6f),
                    Iron = C(0x2c2234, .7f), GateDark = C(0xff8a1c),
                    Pit = new[] { A(255, 170, 60), A(255, 122, 28), A(226, 76, 18), A(244, 98, 22), A(176, 48, 16), A(112, 28, 16), A(134, 34, 16), A(58, 14, 14) },
                    Lip = A(255, 226, 132, 235), CrackEdge = A(255, 168, 56, 215), CrackDark = A(44, 14, 16, 235),
                    Soot = A(28, 12, 16, 165), CraterRim = A(255, 176, 70, 215), CraterDeep = A(70, 20, 14, 235), CraterMid = A(214, 82, 22, 225),
                    CraterCrack = A(255, 132, 38, 225), Clod = A(52, 42, 62, 240)
                };
            }

            // A fortress of ice: pale blue ice flags round a snowflake, walls of blue ice heaped with snow, drifts
            // lying against them, and a blue spire on every corner.
            static Look Frostspire()
            {
                return new Look
                {
                    Index = Frost, Sky = A(22, 40, 104),
                    Void = C(0x1c2f73), Apron = C(0xeef7ff), Grout = C(0x70aee4), TileLight = C(0xb2ddf8), TileDark = C(0x9dd0f4), TileWorn = C(0xd6eeff),
                    WashBlue = C(0x9cc4ff), WashRed = C(0xffd2dc), Wash = .08f,
                    Line = C(0x4f9ee6), LineLight = C(0xffffff), LineDeep = C(0x2f73c8), Seal = C(0xdcf1ff),
                    Stain = C(0xe9f6ff), StainEdge = C(0xd6eeff), PebbleA = C(0xffffff), PebbleB = C(0xe2f2ff), Shade = A(34, 84, 164, 88), Stains = 16,
                    Stone = C(0x68a1ea, .3f), StoneTop = C(0x98c9f7, .35f), StoneShade = C(0x4d82d0, .2f), Plinth = C(0x2f51b0, .15f), PlinthTop = C(0x4f77d4, .15f),
                    Cap = C(0xffffff, .2f), Block = C(0xf6fbff, .2f), BlockDark = C(0xc4e8ff, .5f), Trim = C(Kit.Gold, .9f), Bowl = C(0xd48a1c, .8f), Pole = C(0x5f7fc8, .2f),
                    StandRed = new[] { C(0x84608f), C(0x654772), C(0xae8cb8) }, StandBlue = new[] { C(0x5d8dd6), C(0x446eb8), C(0x90baf0) },
                    Stair = C(0xe6f4ff), StairEdge = C(0xb9d9f2), Arcade = C(0x86b8ec), ArcadeTop = C(0xdff1ff), Arch = C(0x1f3a94),
                    Column = C(0xdff1ff), Cornice = C(0xdcecf8), CorniceTop = C(0xffffff), Capital = C(0xffffff),
                    BoxStone = C(0x9cc8f2, .2f), BoxTop = C(0xe6f4ff, .2f), Drape = C(0x2f62d8, .2f), DrapeDeep = C(0x1f44a8, .2f),
                    Curtain = C(0x2f62d8, .25f), CurtainDeep = C(0x1f44a8, .25f),
                    StripeA = C(0x2444b0, .2f), StripeATop = C(0x3560d8, .2f), StripeB = C(0xdcecf8, .2f), StripeBTop = C(0xffffff, .2f),
                    Iron = C(0xbfe0ff, .8f), GateDark = C(0x162a7a),
                    Pit = new[] { A(196, 230, 252), A(152, 202, 242), A(110, 168, 226), A(130, 186, 234), A(80, 134, 206), A(52, 98, 178), A(60, 110, 188), A(26, 52, 122) },
                    Lip = A(255, 255, 255, 235), CrackEdge = A(255, 255, 255, 200), CrackDark = A(40, 86, 164, 235),
                    Soot = A(44, 92, 170, 120), CraterRim = A(255, 255, 255, 225), CraterDeep = A(34, 76, 150, 235), CraterMid = A(98, 152, 216, 225),
                    CraterCrack = A(44, 94, 172, 220), Clod = A(240, 249, 255, 240)
                };
            }

            // A pharaoh's court out in the desert: one plain floor of sand round a golden ankh, what is left of a ring of
            // old paving round that, walls and stands of sandstone, and a stepped pyramid on every corner.
            static Look PharaohSands()
            {
                return new Look
                {
                    Index = Pharaoh, Sky = A(58, 38, 22),
                    // The court is one sand from wall to wall, with no flags to be seen: every tile is the same colour, and
                    // all that is laid over it is a little grain (Sand).
                    Void = C(0x3c2a18), Apron = C(0xe9bd70), Grout = C(0xf0c274), TileLight = C(0xf0c274), TileDark = C(0xf0c274), TileWorn = C(0xf0c274),
                    WashBlue = C(0xf0c274), WashRed = C(0xf0c274), Wash = 0f, ShadeLow = 1f, ShadeHigh = 1f,
                    Line = C(0xd39f56), LineLight = C(0xfff0c2), LineDeep = C(0xb98444), Seal = C(0x1f4fae),
                    Stain = C(0xf0c274), StainEdge = C(0xf0c274), PebbleA = C(0xdcb26a), PebbleB = C(0xb98a4a), Shade = A(128, 72, 18, 84), Stains = 0,
                    Stone = C(0xcf9a4c, .12f), StoneTop = C(0xf2d490, .18f), StoneShade = C(0xa8743a, .1f), Plinth = C(0xdba95c, .05f), PlinthTop = C(0xf3d28c, .05f),
                    Cap = C(0x1fb5a8, .6f), Block = C(0xf2d490, .2f), BlockDark = C(0xcf9a4c, .2f), Trim = C(Kit.Gold, .9f), Bowl = C(0x3a2a20, .5f), Pole = C(0x8a5a2a, .1f),
                    // The stands are cut from sandstone too; only the lip of each row is painted in its team's colour.
                    StandRed = new[] { C(0xd0935a), C(0xa3683a), C(0xd9603f) }, StandBlue = new[] { C(0xcfa466), C(0xa27a42), C(0x3a72d4) },
                    Stair = C(0xf6e2aa), StairEdge = C(0xcfa862), Arcade = C(0xcf9a4c), ArcadeTop = C(0xf0d284), Arch = C(0x3a2412),
                    Column = C(0xf6e2a4), Cornice = C(0x1f4fae), CorniceTop = C(0x3a72d4), Capital = C(Kit.Gold),
                    BoxStone = C(0xdcae58, .1f), BoxTop = C(0xf6dc92, .1f), Drape = C(0x1f4fae, .2f), DrapeDeep = C(0x173a86, .2f),
                    Curtain = C(0x1f4fae, .25f), CurtainDeep = C(0x173a86, .25f),
                    StripeA = C(0x1f4fae, .2f), StripeATop = C(0x3a72d4, .2f), StripeB = C(Kit.Gold, .9f), StripeBTop = C(0xffd76a, .6f),
                    Iron = C(Kit.Gold, .9f), GateDark = C(0x2a1a10),
                    Pit = new[] { A(214, 170, 90), A(176, 132, 66), A(138, 100, 52), A(156, 116, 58), A(108, 78, 44), A(78, 56, 36), A(88, 62, 38), A(38, 26, 22) },
                    Lip = A(255, 240, 190, 235), CrackEdge = A(255, 236, 180, 170), CrackDark = A(70, 44, 18, 235),
                    Soot = A(96, 60, 20, 150), CraterRim = A(255, 232, 160, 210), CraterDeep = A(64, 40, 16, 235), CraterMid = A(150, 104, 44, 225),
                    CraterCrack = A(66, 40, 16, 220), Clod = A(190, 146, 70, 235)
                };
            }

            // A temple the jungle has taken back: mossy green flags round a golden leaf, sandstone walls inlaid
            // with jade and grown over with leaves, creepers on the floor, and a cut jade on every corner.
            static Look JadeTemple()
            {
                return new Look
                {
                    Index = Jade, Sky = A(14, 40, 30),
                    Void = C(0x12301f), Apron = C(0x3f8f3a), Grout = C(0x4f9230), TileLight = C(0x93bd6a), TileDark = C(0x84b05c), TileWorn = C(0xd9cf9a),
                    WashBlue = C(0xbfe0ff), WashRed = C(0xffdcc0), Wash = .07f, ShadeLow = .94f, ShadeHigh = 1.04f,
                    Line = C(0xd9c27a), LineLight = C(0xfff3c4), LineDeep = C(0xa8924e), Seal = C(0x1f8a5c),
                    Stain = C(0x6fa04a), StainEdge = C(0x7cab56), PebbleA = C(0xe6dcb4), PebbleB = C(0x6fa046), Shade = A(22, 62, 26, 104), Stains = 26,
                    Stone = C(0xcdb98a, .12f), StoneTop = C(0xeadfb8, .15f), StoneShade = C(0xa8956a, .1f), Plinth = C(0x5f8a3a, .1f), PlinthTop = C(0x86b052, .1f),
                    Cap = C(0x2fc48a, .7f), Block = C(0xe6dcb8, .2f), BlockDark = C(0xcbbd90, .2f), Trim = C(Kit.Gold, .9f), Bowl = C(0x3a4a34, .5f), Pole = C(0x7a5230, .1f),
                    StandRed = new[] { C(0xb5653f), C(0x8a4a30), C(0xd98a5e) }, StandBlue = new[] { C(0x3f8f8a), C(0x2c6f6c), C(0x6ab8b0) },
                    Stair = C(0xe6dcb4), StairEdge = C(0xbfae82), Arcade = C(0xbfa878), ArcadeTop = C(0xe6d9ae), Arch = C(0x1f3a26),
                    Column = C(0xe6dcb8), Cornice = C(0x2a9d74), CorniceTop = C(0x4fd0a0), Capital = C(Kit.Gold),
                    BoxStone = C(0xcdb98a, .1f), BoxTop = C(0xeadfb8, .1f), Drape = C(0x1f9d5a, .2f), DrapeDeep = C(0x157a44, .2f),
                    Curtain = C(0x1f9d5a, .25f), CurtainDeep = C(0x157a44, .25f),
                    StripeA = C(0x1a8a52, .2f), StripeATop = C(0x2fb970, .2f), StripeB = C(0xf1e6c0, .2f), StripeBTop = C(0xfff6dc, .2f),
                    Iron = C(Kit.Gold, .9f), GateDark = C(0x1c3a26),
                    Pit = new[] { A(150, 120, 70), A(120, 94, 56), A(92, 72, 46), A(106, 84, 52), A(74, 58, 40), A(54, 44, 34), A(62, 50, 36), A(30, 26, 24) },
                    Lip = A(244, 240, 200, 235), CrackEdge = A(240, 238, 196, 170), CrackDark = A(44, 52, 28, 235),
                    Soot = A(40, 56, 24, 150), CraterRim = A(236, 232, 180, 210), CraterDeep = A(46, 40, 24, 235), CraterMid = A(104, 92, 50, 225),
                    CraterCrack = A(44, 40, 22, 220), Clod = A(120, 150, 70, 235)
                };
            }

            // A football field: mown turf marked with yard lines round a football, an end zone in each team's
            // colour, padded walls with pylons along them, a running track outside, and a floodlight on every corner.
            static Look GridironBowl()
            {
                return new Look
                {
                    Index = Gridiron, Sky = A(18, 28, 44),
                    Void = C(0x16202e), Apron = C(0xb4533a), Grout = C(0x44a340), TileLight = C(0x44a340), TileDark = C(0x44a340), TileWorn = C(0x44a340),
                    WashBlue = C(0x44a340), WashRed = C(0x44a340), Wash = 0f, ShadeLow = 1f, ShadeHigh = 1f,
                    Line = C(0xffffff), LineLight = C(0xffffff), LineDeep = C(0xd8e2d8), Seal = C(0x1c2f5e),
                    Stain = C(0x44a340), StainEdge = C(0x44a340), PebbleA = C(0xf4f6fa), PebbleB = C(0xe2e6ee), Shade = A(10, 40, 16, 96), Stains = 0,
                    Stone = C(0xe6e9ee, .2f), StoneTop = C(0xffffff, .2f), StoneShade = C(0xb9c0cc, .1f), Plinth = C(0x24365f, .2f), PlinthTop = C(0x3a5290, .2f),
                    Cap = C(0xf7c545, .7f), Block = C(0xffffff, .2f), BlockDark = C(0xd8dde6, .2f), Trim = C(Kit.Gold, .9f), Bowl = C(0x24365f, .5f), Pole = C(0xc9ced8, .5f),
                    StandRed = new[] { C(0xc8372f), C(0x9a2a26), C(0xe86a5c) }, StandBlue = new[] { C(0x2f62c8), C(0x224a9a), C(0x5a8ae6) },
                    Stair = C(0xd8dde6), StairEdge = C(0xaab2c0), Arcade = C(0x9aa3b4), ArcadeTop = C(0xc9d0dc), Arch = C(0x1c2436),
                    Column = C(0xe6e9ee), Cornice = C(0x24365f), CorniceTop = C(0x3a5290), Capital = C(Kit.Gold),
                    BoxStone = C(0xcfd4dc, .1f), BoxTop = C(0xf4f6fa, .1f), Drape = C(0x24365f, .2f), DrapeDeep = C(0x18264a, .2f),
                    Curtain = C(0x24365f, .25f), CurtainDeep = C(0x18264a, .25f),
                    StripeA = C(0x24365f, .2f), StripeATop = C(0x3a5290, .2f), StripeB = C(0xf4f6fa, .2f), StripeBTop = C(0xffffff, .2f),
                    Iron = C(0xf7c545, .8f), GateDark = C(0x101826),
                    Pit = new[] { A(120, 92, 60), A(96, 72, 48), A(74, 56, 40), A(84, 64, 44), A(58, 44, 34), A(42, 32, 28), A(48, 36, 30), A(22, 18, 18) },
                    Lip = A(235, 255, 235, 235), CrackEdge = A(220, 250, 220, 170), CrackDark = A(30, 50, 24, 235),
                    Soot = A(34, 60, 24, 150), CraterRim = A(214, 240, 190, 210), CraterDeep = A(52, 38, 24, 235), CraterMid = A(110, 84, 50, 225),
                    CraterCrack = A(48, 36, 22, 220), Clod = A(60, 140, 56, 235)
                };
            }
        }

        static Look look = Look.For(1);

        /// <summary>The look the arena has now, for whatever else is made of the same stone (the rubble of an earthquake).</summary>
        internal static Look Skin { get { return look; } }

        /// <summary>The number of the look the arena has now: it changes whenever the arena is dressed for another map.</summary>
        public int LookIndex { get { return look.Index; } }

        /// <summary>What the camera should clear to behind this arena.</summary>
        public Color32 Sky { get { return look.Sky; } }

        Transform scenery;

        /// <summary>
        /// Dresses the arena for the given arena of the journey. Everything that is only there to be seen is built
        /// again in that map's look; the colliders, the crowd and the emperor stay as they are. True if the look changed.
        /// </summary>
        public bool Dress(int stageNumber)
        {
            Look wanted = Look.For(stageNumber);
            if (wanted == look && scenery != null) return false;
            look = wanted;
            if (scenery != null)
            {
                foreach (MeshFilter filter in scenery.GetComponentsInChildren<MeshFilter>(true)) Object.Destroy(filter.sharedMesh);
                scenery.gameObject.SetActive(false);
                Object.Destroy(scenery.gameObject);
            }
            sways.Clear();
            flames.Clear();
            sideFlags.Clear();
            BuildScenery();
            RaiseWalls(sideWalls != null ? sideWalls.Cuts : new WallCut[0]);
            return true;
        }

        void BuildScenery()
        {
            scenery = new GameObject("Scenery").transform;
            scenery.SetParent(wallRoot, false);
            BuildCourt(scenery, wallHw, wallHd);
            BuildWalls(scenery, wallHw, wallHd);
            BuildStands(scenery, wallHw, wallHd);
            BuildEmperorBox(scenery, wallHd);
            BuildGatesAndBanners(scenery, wallHw, wallHd);
        }

        // ------------------------------------------------------------------ paint laid flat on the court

        static void Flat(MeshKit kit, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 paint)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f) kit.Quad(a, d, c, b, paint); else kit.Quad(a, b, c, d, paint);
        }

        static void FlatTri(MeshKit kit, Vector3 a, Vector3 b, Vector3 c, Color32 paint)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f) kit.Tri(a, c, b, paint); else kit.Tri(a, b, c, paint);
        }

        /// <summary>A straight stroke of paint on the ground, of the given width.</summary>
        static void Stroke(MeshKit kit, Vector3 from, Vector3 to, float wide, Color32 paint)
        {
            Vector3 along = to - from;
            if (along.sqrMagnitude < 1e-8f) return;
            Vector3 side = Vector3.Cross(Vector3.up, along.normalized) * (wide * .5f);
            Flat(kit, from - side, to - side, to + side, from + side, paint);
        }

        /// <summary>What is painted in the middle of the court: every map has its own seal.</summary>
        static void PaintSeal(MeshKit kit, float lift)
        {
            Look l = look;
            Color32 line = l.Line, lineLight = l.LineLight;
            if (l.Index == Look.Sandpit)
            {
                kit.Disc(new Vector3(0f, lift, 0f), 1.72f, 1.72f, line, line, 48);
                kit.Disc(new Vector3(0f, lift + .003f, 0f), 1.58f, 1.58f, l.Seal, l.Seal, 48);
                kit.Ring(new Vector3(0f, lift + .006f, 0f), 1.18f, 1.28f, line, 48);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8f, reach = i % 2 == 0 ? 1.12f : .62f;
                    var tip = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * reach;
                    var left = new Vector3(Mathf.Cos(a - .28f), 0f, Mathf.Sin(a - .28f)) * .3f;
                    var right = new Vector3(Mathf.Cos(a + .28f), 0f, Mathf.Sin(a + .28f)) * .3f;
                    Vector3 up = Vector3.up * (lift + .009f);
                    kit.Tri(up, right + up, tip + up, i % 2 == 0 ? line : lineLight);
                    kit.Tri(up, tip + up, left + up, i % 2 == 0 ? l.LineDeep : line);
                }
                kit.Disc(new Vector3(0f, lift + .012f, 0f), .2f, .2f, C(Kit.Gold), C(Kit.GoldDark), 20);
                return;
            }

            // The other maps share a broad golden ring round a dark ground, with the map's own device inside it.
            Color32 gold = C(Kit.Gold), goldDeep = C(Kit.GoldDark), goldLight = C(0xffdf7a);
            bool frost = l.Index == Look.Frost;
            Color32 band = frost ? C(0x3f8fe0) : gold, bandDeep = frost ? C(0x2a6fc8) : goldDeep, bandLight = frost ? C(0x8cc8ff) : goldLight;
            kit.Disc(new Vector3(0f, lift, 0f), 1.80f, 1.80f, bandDeep, bandDeep, 56);
            kit.Disc(new Vector3(0f, lift + .003f, 0f), 1.74f, 1.74f, band, band, 56);
            kit.Ring(new Vector3(0f, lift + .005f, 0f), 1.66f, 1.70f, bandLight, 56);
            kit.Disc(new Vector3(0f, lift + .006f, 0f), 1.50f, 1.50f, bandDeep, bandDeep, 56);
            kit.Disc(new Vector3(0f, lift + .009f, 0f), 1.45f, 1.45f, l.Seal, l.Seal, 56);
            float y = lift + .012f;
            switch (l.Index)
            {
                case Look.Tidal:
                    kit.Ring(new Vector3(0f, y, 0f), 1.30f, 1.34f, C(0x39b4b6), 48);
                    Anchor(kit, y + .002f, 1.12f, .05f, goldDeep);
                    Anchor(kit, y + .005f, 1.12f, 0f, gold);
                    // The four points of the compass, as pale lozenges outside the ring.
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * .5f;
                        Vector3 out1 = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), across = new Vector3(-out1.z, 0f, out1.x);
                        Vector3 at = out1 * 2.28f + Vector3.up * (lift + .003f);
                        Flat(kit, at - out1 * .3f, at + across * .19f, at + out1 * .3f, at - across * .19f, C(0xfbeecb));
                    }
                    break;
                case Look.Ember:
                    kit.Ring(new Vector3(0f, y, 0f), 1.30f, 1.34f, C(0x5d4f6a), 48);
                    Flame(kit, y + .002f, 1.20f, new Vector2(0f, -.02f), C(0xb23412));
                    Flame(kit, y + .005f, 1.10f, new Vector2(0f, -.04f), C(0xff6a14));
                    Flame(kit, y + .008f, .74f, new Vector2(.01f, -.28f), C(0xffb224));
                    Flame(kit, y + .011f, .42f, new Vector2(.02f, -.50f), C(0xffe873));
                    break;
                case Look.Pharaoh:
                    kit.Ring(new Vector3(0f, y, 0f), 1.30f, 1.34f, C(0x35c4b4), 48);
                    Ankh(kit, y + .002f, 1.12f, .05f, goldDeep);
                    Ankh(kit, y + .005f, 1.12f, 0f, gold);
                    break;
                case Look.Jade:
                    kit.Ring(new Vector3(0f, y, 0f), 1.30f, 1.34f, C(0x58d0a0), 48);
                    Leaf(kit, y + .002f, 1.12f, .05f, goldDeep);
                    Leaf(kit, y + .005f, 1.12f, 0f, gold);
                    LeafVeins(kit, y + .008f, 1.12f, goldDeep);
                    break;
                case Look.Gridiron:
                    kit.Ring(new Vector3(0f, y, 0f), 1.30f, 1.34f, C(0xffffff), 48);
                    Football(kit, y + .002f, 1.08f);
                    break;
                default:
                    kit.Ring(new Vector3(0f, y, 0f), 1.30f, 1.34f, C(0xa9d6fb), 48);
                    Snowflake(kit, y + .002f, 1.20f, .05f, C(0x2a6fc8));
                    Snowflake(kit, y + .005f, 1.20f, 0f, C(0x4d9df0));
                    break;
            }
        }

        /// <summary>A ship's anchor, its ring to the north. `grow` thickens every part of it, for an edge laid underneath.</summary>
        static void Anchor(MeshKit kit, float y, float size, float grow, Color32 paint)
        {
            Vector3 P(float x, float z) { return new Vector3(x * size, y, z * size); }
            float g = grow;
            kit.Ring(P(0f, .84f), (.11f - g * .5f) * size, (.25f + g) * size, paint, 20);
            Flat(kit, P(-.085f - g, -.72f), P(-.085f - g, .62f), P(.085f + g, .62f), P(.085f + g, -.72f), paint);
            Flat(kit, P(-.44f - g, .35f - g), P(-.44f - g, .49f + g), P(.44f + g, .49f + g), P(.44f + g, .35f - g), paint);
            kit.Disc(P(-.46f, .42f), (.105f + g) * size, (.105f + g) * size, paint, paint, 12);
            kit.Disc(P(.46f, .42f), (.105f + g) * size, (.105f + g) * size, paint, paint, 12);
            const float from = 205f * Mathf.Deg2Rad, to = 335f * Mathf.Deg2Rad, reach = .72f;
            kit.Ring(P(0f, 0f), (reach - .085f - g) * size, (reach + .085f + g) * size, paint, 22, from, to);
            for (int end = 0; end < 2; end++)
            {
                float a = end == 0 ? from : to, turn = end == 0 ? -1f : 1f;
                var radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 along = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * turn, at = radial * reach - along * .05f;
                Vector2 outer = at + radial * (.2f + g), inner = at - radial * (.2f + g), tip = at + along * (.36f + g * 1.4f);
                FlatTri(kit, P(outer.x, outer.y), P(inner.x, inner.y), P(tip.x, tip.y), paint);
            }
            FlatTri(kit, P(-.16f - g, -.70f), P(.16f + g, -.70f), P(0f, -.95f - g * 1.4f), paint);
        }

        // The rim of a flame with three tongues, going round it from its foot; every point can be seen from the hub.
        static readonly Vector2[] FlameRim =
        {
            new Vector2(0f, -.90f), new Vector2(.38f, -.82f), new Vector2(.60f, -.58f), new Vector2(.68f, -.28f), new Vector2(.62f, .18f),
            new Vector2(.34f, .02f), new Vector2(.30f, .35f), new Vector2(.20f, .66f), new Vector2(.06f, .92f), new Vector2(.02f, 1.02f),
            new Vector2(-.14f, .74f), new Vector2(-.24f, .46f), new Vector2(-.50f, .50f), new Vector2(-.58f, .22f), new Vector2(-.68f, -.10f),
            new Vector2(-.66f, -.42f), new Vector2(-.52f, -.70f), new Vector2(-.28f, -.86f)
        };

        static void Flame(MeshKit kit, float y, float size, Vector2 shift, Color32 paint)
        {
            Vector3 P(Vector2 point) { return new Vector3(point.x * size + shift.x, y, point.y * size + shift.y); }
            var hub = new Vector2(0f, -.3f);
            for (int i = 0; i < FlameRim.Length; i++)
                FlatTri(kit, P(hub), P(FlameRim[i]), P(FlameRim[(i + 1) % FlameRim.Length]), paint);
        }

        static void Snowflake(MeshKit kit, float y, float size, float grow, Color32 paint)
        {
            Vector3 P(Vector2 point) { return new Vector3(point.x * size, y, point.y * size); }
            for (int arm = 0; arm < 6; arm++)
            {
                float a = (arm * 60f + 30f) * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Stroke(kit, P(Vector2.zero), P(along * (1.0f + grow)), (.13f + grow * 2f) * size, paint);
                var across = new Vector2(-along.y, along.x);
                Vector2 tip = along * (1.0f + grow);
                FlatTri(kit, P(tip - across * (.065f + grow)), P(tip + across * (.065f + grow)), P(tip + along * (.1f + grow)), paint);
                for (int twig = 0; twig < 2; twig++)
                {
                    float where = twig == 0 ? .46f : .74f, reach = (twig == 0 ? .33f : .23f) + grow;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float b = a + side * 60f * Mathf.Deg2Rad;
                        Stroke(kit, P(along * where), P(along * where + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * reach), (.1f + grow * 2f) * size, paint);
                    }
                }
            }
            kit.Disc(new Vector3(0f, y, 0f), (.2f + grow) * size, (.2f + grow) * size, paint, paint, 6);
        }

        /// <summary>An ankh, its loop to the north. `grow` thickens every part of it, for an edge laid underneath.</summary>
        static void Ankh(MeshKit kit, float y, float size, float grow, Color32 paint)
        {
            Vector3 P(float x, float z) { return new Vector3(x * size, y, z * size); }
            float g = grow, wide = .09f + g;
            // The loop, taller than it is broad.
            const int seg = 24;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var d0 = new Vector2(Mathf.Cos(a0) * .29f, Mathf.Sin(a0) * .39f);
                var d1 = new Vector2(Mathf.Cos(a1) * .29f, Mathf.Sin(a1) * .39f);
                Vector2 n0 = d0.normalized * wide, n1 = d1.normalized * wide;
                Flat(kit, P(d0.x - n0.x, .52f + d0.y - n0.y), P(d1.x - n1.x, .52f + d1.y - n1.y), P(d1.x + n1.x, .52f + d1.y + n1.y), P(d0.x + n0.x, .52f + d0.y + n0.y), paint);
            }
            // The stem, broader at its foot, and the two arms, broader at their ends.
            Flat(kit, P(-.09f - g, .14f), P(.09f + g, .14f), P(.14f + g, -.96f - g), P(-.14f - g, -.96f - g), paint);
            for (int side = -1; side <= 1; side += 2)
                Flat(kit, P(0f, -.01f - g), P(0f, .15f + g), P(side * (.56f + g), .2f + g), P(side * (.56f + g), -.06f - g), paint);
        }

        // The rim of a leaf, going round it from the foot of its stalk; every point can be seen from the hub.
        static readonly Vector2[] LeafRim =
        {
            new Vector2(0f, -.74f), new Vector2(.22f, -.62f), new Vector2(.40f, -.40f), new Vector2(.52f, -.12f), new Vector2(.55f, .16f),
            new Vector2(.48f, .44f), new Vector2(.35f, .68f), new Vector2(.19f, .88f), new Vector2(0f, 1.04f),
            new Vector2(-.19f, .88f), new Vector2(-.35f, .68f), new Vector2(-.48f, .44f), new Vector2(-.55f, .16f), new Vector2(-.52f, -.12f),
            new Vector2(-.40f, -.40f), new Vector2(-.22f, -.62f)
        };

        /// <summary>A broad leaf, its point to the north. `grow` thickens it, for an edge laid underneath.</summary>
        static void Leaf(MeshKit kit, float y, float size, float grow, Color32 paint)
        {
            Vector3 P(Vector2 point) { return new Vector3(point.x * size, y, point.y * size); }
            var hub = new Vector2(0f, .15f);
            Vector2 Out(Vector2 point) { return point + (point - hub).normalized * grow; }
            for (int i = 0; i < LeafRim.Length; i++)
                FlatTri(kit, P(hub), P(Out(LeafRim[i])), P(Out(LeafRim[(i + 1) % LeafRim.Length])), paint);
            Stroke(kit, P(new Vector2(0f, -.68f)), P(new Vector2(0f, -1.02f - grow)), (.1f + grow * 2f) * size, paint);
        }

        /// <summary>The ribs of that leaf, laid over it in a darker gold.</summary>
        static void LeafVeins(MeshKit kit, float y, float size, Color32 paint)
        {
            Vector3 P(float x, float z) { return new Vector3(x * size, y, z * size); }
            Stroke(kit, P(0f, -.66f), P(0f, .9f), .055f * size, paint);
            float[] from = { -.38f, -.08f, .24f, .52f }, reach = { .36f, .42f, .36f, .24f };
            for (int i = 0; i < from.Length; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                    Stroke(kit, P(0f, from[i]), P(side * reach[i], from[i] + .22f), .04f * size, paint);
            }
        }

        /// <summary>A football lying aslant: brown leather with a dark edge, a white band near either end, and the laces.</summary>
        static void Football(MeshKit kit, float y, float size)
        {
            Color32 edge = C(0x5a3218), leather = C(0x9a5a2c), white = C(0xffffff);
            const float turn = 28f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(turn), sin = Mathf.Sin(turn);
            Vector3 P(float along, float across, float lift)
            {
                return new Vector3((along * cos - across * sin) * size, y + lift, (along * sin + across * cos) * size);
            }
            // How broad the ball is this far along it (-1 at one point, 1 at the other).
            float Half(float t) { return .5f * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(t * Mathf.PI * .5f)), .8f); }
            const int seg = 18;
            for (int layer = 0; layer < 2; layer++)
            {
                float grow = layer == 0 ? .055f : 0f, lift = layer * .003f, longer = 1f + grow;
                Color32 paint = layer == 0 ? edge : leather;
                for (int i = 0; i < seg; i++)
                {
                    float t0 = -1f + 2f * i / seg, t1 = -1f + 2f * (i + 1) / seg;
                    float w0 = Half(t0) + (i == 0 ? 0f : grow), w1 = Half(t1) + (i == seg - 1 ? 0f : grow);
                    Flat(kit, P(t0 * longer, -w0, lift), P(t1 * longer, -w1, lift), P(t1 * longer, w1, lift), P(t0 * longer, w0, lift), paint);
                }
            }
            for (int end = -1; end <= 1; end += 2)
            {
                float t0 = end * .58f, t1 = end * .7f;
                Flat(kit, P(t0, -Half(t0), .006f), P(t1, -Half(t1), .006f), P(t1, Half(t1), .006f), P(t0, Half(t0), .006f), white);
            }
            Flat(kit, P(-.3f, -.03f, .008f), P(.3f, -.03f, .008f), P(.3f, .03f, .008f), P(-.3f, .03f, .008f), white);
            for (int i = -2; i <= 2; i++)
                Flat(kit, P(i * .12f - .025f, -.11f, .009f), P(i * .12f + .025f, -.11f, .009f), P(i * .12f + .025f, .11f, .009f), P(i * .12f - .025f, .11f, .009f), white);
        }

        /// <summary>A painted border round the court, a little in from the walls: a broad line, and a thin one inside it.</summary>
        static void Border(MeshKit kit, float hw, float hd, float y, float inset, Color32 broad, Color32 thin)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                float wide = pass == 0 ? .1f : .04f, x = hw - inset - pass * .16f, z = hd - inset - pass * .16f, more = wide * .5f;
                Color32 paint = pass == 0 ? broad : thin;
                for (int side = -1; side <= 1; side += 2)
                {
                    Stroke(kit, new Vector3(-x - more, y, side * z), new Vector3(x + more, y, side * z), wide, paint);
                    Stroke(kit, new Vector3(side * x, y, -z - more), new Vector3(side * x, y, z + more), wide, paint);
                }
            }
        }

        /// <summary>Creepers on the flags of the jungle temple: each comes in from the foot of a wall, in leaf, and some end in a flower.</summary>
        static void Creepers(MeshKit kit, float hw, float hd, float y)
        {
            Color32 stem = C(0x2f7a2c), leaf = C(0x4fa83c), leafLight = C(0x7cc552), petal = C(0xff9a2e), heart = C(0xffe066);
            // Broad leaves hanging in over the foot of the walls, the way the jungle leans over a clearing.
            Color32 broad = C(0x2f8f3c), broadLight = C(0x45a84a), rib = C(0x7fcf62);
            for (int n = 0; n < 18; n++)
            {
                bool ends = Random.value < .3f;
                float sign = Random.value < .5f ? -1f : 1f, long1 = Random.Range(.55f, .95f);
                Vector2 foot = ends ? new Vector2(Random.Range(-hw + .5f, hw - .5f), sign * hd) : new Vector2(sign * hw, Random.Range(-hd + .5f, hd - .5f));
                // Clear of the half-way line.
                if (Mathf.Abs(foot.y) < .9f) continue;
                float way = ((ends ? (sign > 0f ? -90f : 90f) : (sign > 0f ? 180f : 0f)) + Random.Range(-34f, 34f)) * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(way), Mathf.Sin(way));
                var across = new Vector2(-along.y, along.x);
                float scale = long1 / 1.78f;
                Vector3 At(Vector2 point, float lift1)
                {
                    Vector2 flat = foot + across * (point.x * scale) + along * ((point.y + .74f) * scale);
                    return new Vector3(flat.x, y + lift1, flat.y);
                }
                var hub = new Vector2(0f, .15f);
                Color32 paint = n % 3 == 0 ? broadLight : broad;
                for (int i = 0; i < LeafRim.Length; i++)
                    FlatTri(kit, At(hub, .002f), At(LeafRim[i], .002f), At(LeafRim[(i + 1) % LeafRim.Length], .002f), paint);
                Stroke(kit, At(new Vector2(0f, -.74f), .003f), At(new Vector2(0f, .92f), .003f), .045f, rib);
            }
            for (int n = 0; n < 18; n++)
            {
                bool ends = Random.value < .3f;
                float sign = Random.value < .5f ? -1f : 1f;
                Vector2 at = ends ? new Vector2(Random.Range(-hw + .6f, hw - .6f), sign * (hd - .05f)) : new Vector2(sign * (hw - .05f), Random.Range(-hd + .6f, hd - .6f));
                // It sets out straight into the court, and wanders as it goes.
                float heading = ends ? (sign > 0f ? -90f : 90f) : (sign > 0f ? 180f : 0f);
                int pieces = Random.Range(3, 6);
                for (int i = 0; i < pieces; i++)
                {
                    heading += Random.Range(-38f, 38f);
                    float way = heading * Mathf.Deg2Rad;
                    Vector2 next = at + new Vector2(Mathf.Cos(way), Mathf.Sin(way)) * Random.Range(.22f, .36f);
                    // Kept clear of the seal and its band, of the half-way line, and inside the walls.
                    if (next.magnitude < 3.1f || Mathf.Abs(next.y) < .35f || Mathf.Abs(next.x) > hw - .04f || Mathf.Abs(next.y) > hd - .04f) break;
                    Stroke(kit, new Vector3(at.x, y, at.y), new Vector3(next.x, y, next.y), .07f, stem);
                    // A leaf to one side at every joint, and to the other at the next.
                    float lean = way + (i % 2 == 0 ? 1.1f : -1.1f);
                    Vector2 tip = next + new Vector2(Mathf.Cos(lean), Mathf.Sin(lean)) * .27f, side = new Vector2(-Mathf.Sin(lean), Mathf.Cos(lean)) * .095f, mid = (next + tip) * .5f;
                    FlatTri(kit, new Vector3(next.x, y + .001f, next.y), new Vector3(mid.x + side.x, y + .001f, mid.y + side.y), new Vector3(tip.x, y + .001f, tip.y), leaf);
                    FlatTri(kit, new Vector3(next.x, y + .001f, next.y), new Vector3(tip.x, y + .001f, tip.y), new Vector3(mid.x - side.x, y + .001f, mid.y - side.y), leafLight);
                    at = next;
                }
                if (Random.value > .55f) continue;
                var spot = new Vector3(at.x, y + .002f, at.y);
                for (int p = 0; p < 5; p++)
                {
                    float a = p * Mathf.PI * .4f;
                    kit.Disc(spot + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * .075f, .065f, .065f, petal, petal, 8);
                }
                kit.Disc(spot + Vector3.up * .001f, .05f, .05f, heart, heart, 8);
            }
        }

        /// <summary>
        /// The markings of a football field: stripes of mown turf, an end zone in each team's colour (blue to the
        /// south, red to the north), the yard lines with their hash marks, and the sidelines. No numbers.
        /// </summary>
        static void Field(MeshKit kit, float hw, float hd, float y)
        {
            Color32 mown = C(0x3a9238), white = C(0xffffff), blue = C(0x2a62c8), red = C(0xc93a30);
            const float zone = 2.4f, edge = .3f;
            float goal = hd - zone, step = goal / 4f, under = y - .002f;
            for (int k = -4; k < 4; k += 2)
                kit.Floor(-hw, k * step, hw, (k + 1) * step, under, mown);
            kit.Floor(-hw, -hd, hw, -goal, under, blue);
            kit.Floor(-hw, goal, hw, hd, under, red);
            // Slanting bars across each end zone, a shade lighter than it.
            for (int end = -1; end <= 1; end += 2)
            {
                Color32 pale = MeshKit.Mix(end < 0 ? blue : red, white, .22f);
                for (int i = 0; i < 9; i++)
                {
                    float x = -hw + .5f + i * (hw * 2f - 1f) / 8f;
                    Stroke(kit, new Vector3(x - .35f, under + .001f, end * (goal + .35f)), new Vector3(x + .35f, under + .001f, end * (hd - .5f)), .16f, pale);
                }
            }
            float left = -hw + edge, right = hw - edge;
            for (int k = -4; k <= 4; k++)
            {
                if (k == 0) continue;
                Stroke(kit, new Vector3(left, y, k * step), new Vector3(right, y, k * step), Mathf.Abs(k) == 4 ? .11f : .06f, white);
            }
            // Four hash marks to every stretch between two lines: a pair in the field, and one on each sideline.
            for (int j = -19; j <= 19; j++)
            {
                if (j % 5 == 0) continue;
                float z = j * step / 5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Stroke(kit, new Vector3(side * 1.4f, y, z), new Vector3(side * 1.68f, y, z), .04f, white);
                    Stroke(kit, new Vector3(side * (right - .06f), y, z), new Vector3(side * (right - .26f), y, z), .04f, white);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Stroke(kit, new Vector3(side * right, y, -hd + edge - .045f), new Vector3(side * right, y, hd - edge + .045f), .09f, white);
                Stroke(kit, new Vector3(left, y, side * (hd - edge)), new Vector3(right, y, side * (hd - edge)), .09f, white);
            }
        }

        /// <summary>A goal post standing on the middle of an end wall: a pole, a crossbar and two uprights, in yellow.</summary>
        static void GoalPost(MeshKit kit, Vector3 foot, float uprights)
        {
            Color32 yellow = C(0xffd21f, .7f), deep = C(0xd9a514, .6f);
            float bar = WallHigh + .8f;
            kit.Capsule(foot + Vector3.up * (WallHigh - .1f), foot + Vector3.up * bar, .1f, .08f, deep, 8);
            kit.Capsule(foot + new Vector3(-1.5f, bar, 0f), foot + new Vector3(1.5f, bar, 0f), .075f, .075f, yellow, 8);
            for (int side = -1; side <= 1; side += 2)
            {
                kit.Capsule(foot + new Vector3(side * 1.5f, bar, 0f), foot + new Vector3(side * 1.5f, bar + uprights, 0f), .07f, .055f, yellow, 8);
                kit.Ball(foot + new Vector3(side * 1.5f, bar + uprights + .04f, 0f), .075f, C(0xff7a1a, .6f), 6);
            }
        }

        /// <summary>A palm: a trunk in rings, leaning a little, and a crown of fronds as long as `reach`. A longer reach makes all of it stouter.</summary>
        static void Palm(MeshKit kit, Vector3 foot, float tall, float reach = .52f)
        {
            Color32 bark = C(0x9a6a3a, .1f), barkDeep = C(0x7a4f2a, .1f), frond = C(0x3fa03a, .2f), frondLight = C(0x6cc44a, .2f);
            float stout = reach / .52f;
            Vector3 at = foot;
            for (int i = 0; i < 5; i++)
            {
                Vector3 next = foot + new Vector3(Mathf.Sin(i * .5f) * .07f * stout, tall * (i + 1) / 5f, 0f);
                kit.Capsule(at, next, (.08f - i * .007f) * stout, (.075f - i * .007f) * stout, i % 2 == 0 ? bark : barkDeep, 6);
                at = next;
            }
            for (int m = 0; m < 7; m++)
            {
                float a = m * Mathf.PI * 2f / 7f;
                var away = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                kit.Spike(at, at + away * reach + Vector3.up * ((m % 2 == 0 ? .16f : -.04f) * stout), .1f * stout, frond, frondLight, 4);
                // A big crown has a second ring of fronds hanging under the first.
                if (reach > .6f)
                {
                    float b = a + Mathf.PI / 7f;
                    kit.Spike(at, at + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * (reach * .82f) - Vector3.up * (.2f * stout), .09f * stout, frondLight, frond, 4);
                }
            }
            kit.Ball(at, .09f * stout, C(0x6a4a2a, .2f), 6);
        }

        /// <summary>
        /// The floor of the desert court. It is one plain sand from wall to wall, and all that says so is the grain
        /// scattered over it: there are no dunes and no ripples. Round the seal lies what is left of a ring of old
        /// paving, and the half-way line is a groove raked in the sand. All of it is flat paint: nothing is in anyone's way.
        /// </summary>
        static void Sand(MeshKit kit, float hw, float hd, float lift)
        {
            Color32 sand = look.TileLight, dark = MeshKit.Mix(sand, C(0xb07a38), .5f), light = C(0xfde6ae);
            float y = lift - .006f;
            // Grain: little clusters of specks, some darker than the sand and some paler, clear of the seal, its paving
            // and the half-way line.
            for (int n = 0; n < 150; n++)
            {
                var spot = new Vector3(Random.Range(-hw + .35f, hw - .35f), y, Random.Range(-hd + .35f, hd - .35f));
                if (new Vector2(spot.x, spot.z).magnitude < 3.05f || Mathf.Abs(spot.z) < .22f) continue;
                Color32 tone = Random.value < .62f ? dark : light;
                int specks = Random.Range(2, 6);
                for (int k = 0; k < specks; k++)
                {
                    float a = Random.value * Mathf.PI * 2f, reach = Random.Range(0f, .17f), size = Random.Range(.018f, .04f);
                    kit.Disc(spot + new Vector3(Mathf.Cos(a) * reach, 0f, Mathf.Sin(a) * reach * .8f), size, size * Random.Range(.7f, 1f), tone, tone, 6);
                }
            }

            // What is left of the paving that once ringed the seal: three of its slabs are gone.
            Color32 slab = C(0xddb774), slabLight = C(0xe8c684), slabEdge = C(0xb98a46);
            const int slots = 14;
            for (int i = 0; i < slots; i++)
            {
                if (i == 2 || i == 9 || i == 12) continue;
                float mid = (i + .5f) * Mathf.PI * 2f / slots, from = mid - Mathf.PI / slots + .04f, to = mid + Mathf.PI / slots - .04f;
                kit.Ring(new Vector3(0f, y + .002f, 0f), 2.25f, 2.89f, slabEdge, 5, from - .012f, to + .012f);
                kit.Ring(new Vector3(0f, y + .003f, 0f), 2.28f, 2.86f, i % 2 == 0 ? slab : slabLight, 5, from, to);
            }

            // The half-way line is a groove raked in the sand: a bright lip runs along its near side, as far as the seal.
            for (int side = -1; side <= 1; side += 2)
                Stroke(kit, new Vector3(side * 1.86f, lift + .001f, -.095f), new Vector3(side * hw, lift + .001f, -.095f), .04f, C(0xfff0c2));
        }

        /// <summary>What a map lays on its tiles besides the seal: rings of paving, glowing seams, the shine of ice.</summary>
        static void PaintCourt(MeshKit kit, float hw, float hd, float lift)
        {
            Look l = look;
            switch (l.Index)
            {
                case Look.Tidal:
                    // Two rings of darker paving round the seal, the way a harbour square is laid.
                    kit.Ring(new Vector3(0f, lift - .004f, 0f), 2.72f, 2.80f, l.Line, 64);
                    kit.Ring(new Vector3(0f, lift - .004f, 0f), 3.92f, 3.98f, l.Line, 72);
                    break;
                case Look.Ember:
                    Seams(kit, hw, hd, lift - .004f);
                    break;
                case Look.Pharaoh:
                    Sand(kit, hw, hd, lift);
                    break;
                case Look.Jade:
                    // A band of bare sandstone round the seal, and creepers that have come in over the walls.
                    kit.Ring(new Vector3(0f, lift - .004f, 0f), 2.62f, 2.86f, C(0xd9c98e), 64);
                    kit.Ring(new Vector3(0f, lift - .003f, 0f), 2.72f, 2.76f, C(0xb8a468), 64);
                    kit.Ring(new Vector3(0f, lift - .004f, 0f), 3.9f, 4.0f, C(0xd9c98e), 72);
                    Creepers(kit, hw, hd, lift - .004f);
                    break;
                case Look.Gridiron:
                    Field(kit, hw, hd, lift - .004f);
                    break;
                case Look.Frost:
                    // Snow blown in along the foot of the walls.
                    for (int i = 0; i < 26; i++)
                    {
                        bool ends = Random.value < .32f;
                        float sign = Random.value < .5f ? -1f : 1f, deep = Random.Range(.16f, .4f);
                        Vector3 spot = ends
                            ? new Vector3(Random.Range(-hw + .5f, hw - .5f), lift - .004f, sign * (hd - deep * .4f))
                            : new Vector3(sign * (hw - deep * .4f), lift - .004f, Random.Range(-hd + .5f, hd - .5f));
                        float along = Random.Range(.4f, .95f);
                        kit.Disc(spot, ends ? along : deep, ends ? deep : along, C(0xffffff), C(0xeaf6ff), 14);
                    }
                    break;
            }
        }

        /// <summary>Thin seams of fire between the flags of the Ember Crown, kept clear of the seal and the half-way line.</summary>
        static void Seams(MeshKit kit, float hw, float hd, float y)
        {
            Color32 deep = C(0x8f2f18), hot = C(0xff7d1a), core = C(0xffd45a);
            var points = new List<Vector2>();
            int made = 0;
            for (int tries = 0; tries < 60 && made < 9; tries++)
            {
                points.Clear();
                var at = new Vector2(Random.Range(-hw + .6f, hw - .6f), Random.Range(-hd + .7f, hd - .7f));
                float heading = Random.value * Mathf.PI * 2f;
                int pieces = Random.Range(3, 6);
                points.Add(at);
                for (int i = 0; i < pieces; i++)
                {
                    heading += Random.Range(-.95f, .95f);
                    at += new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * Random.Range(.26f, .46f);
                    points.Add(at);
                }
                bool clear = true;
                foreach (Vector2 point in points)
                    clear &= point.magnitude > 2.5f && Mathf.Abs(point.x) < hw - .3f && Mathf.Abs(point.y) < hd - .3f && Mathf.Abs(point.y) > .6f;
                if (!clear) continue;
                made++;
                for (int layer = 0; layer < 3; layer++)
                {
                    Color32 paint = layer == 0 ? deep : layer == 1 ? hot : core;
                    float wide = layer == 0 ? .11f : layer == 1 ? .06f : .022f, height = y + layer * .0015f;
                    for (int i = 0; i < points.Count; i++)
                    {
                        float thin = Mathf.Lerp(.4f, 1f, Mathf.Sin(Mathf.PI * i / (points.Count - 1f)));
                        var here = new Vector3(points[i].x, height, points[i].y);
                        kit.Disc(here, wide * thin * .5f, wide * thin * .5f, paint, paint, 8);
                        if (i == points.Count - 1) break;
                        float next = Mathf.Lerp(.4f, 1f, Mathf.Sin(Mathf.PI * (i + 1) / (points.Count - 1f)));
                        Stroke(kit, here, new Vector3(points[i + 1].x, height, points[i + 1].y), wide * (thin + next) * .5f, paint);
                    }
                }
            }
        }

        /// <summary>The shine on a flag of ice: a pair of pale streaks laid slantwise across it.</summary>
        static void IceShine(MeshKit kit, float x0, float z0, float stepX, float stepZ, float y)
        {
            var pale = C(0xf4fbff);
            float cx = x0 + stepX * Random.Range(.35f, .65f), cz = z0 + stepZ * Random.Range(.35f, .65f), reach = Random.Range(.16f, .26f);
            for (int n = 0; n < 2; n++)
            {
                float shift = n * .13f, part = n == 0 ? 1f : .55f;
                Stroke(kit, new Vector3(cx - reach * part + shift, y, cz - reach * part - shift), new Vector3(cx + reach * part + shift, y, cz + reach * part - shift),
                    n == 0 ? .06f : .04f, pale);
            }
        }

        // ------------------------------------------------------------------ what stands on the walls

        /// <summary>A shape turned on a lathe, standing on `foot`: one flat band of colour between each point of its rim and the next.</summary>
        static void Turned(MeshKit kit, Vector3 foot, Vector2[] rim, Color32[] bands, int seg = 14)
        {
            var profile = new Vector2[(rim.Length - 1) * 2];
            var colors = new Color32[profile.Length];
            for (int i = 0; i < rim.Length - 1; i++)
            {
                profile[i * 2] = rim[i];
                profile[i * 2 + 1] = rim[i + 1];
                colors[i * 2] = colors[i * 2 + 1] = bands[Mathf.Min(i, bands.Length - 1)];
            }
            kit.Lathe(foot, Quaternion.identity, profile, colors, seg);
        }

        /// <summary>A corner tower in the look of the map. The two near towers are kept lower, so as not to hide the court behind them.</summary>
        void BuildTower(MeshKit kit, Transform root, Vector3 corner, bool far)
        {
            Look l = look;
            Color32 gold = l.Trim, goldDeep = C(Kit.GoldDark, .8f);
            switch (l.Index)
            {
                case Look.Tidal:
                {
                    // A lighthouse: banded red and white, a gallery with a gilded rail, the lamp, and a red cap.
                    // The two far ones stand tall over the stands; the near ones are stubs of the same.
                    Color32 white = C(0xfbf2de, .2f), red = C(0xe2493a, .25f), cap = C(0xd23e30, .25f), lamp = C(0xfff3a8, 1f);
                    int bands = far ? 4 : 3;
                    float top = far ? 1.98f : 1.08f, band = (top - .22f) / bands, slim = far ? .11f : .06f;
                    var rim = new List<Vector2> { new Vector2(.70f, 0f), new Vector2(.68f, .22f), new Vector2(.64f, .22f) };
                    var paint = new List<Color32> { l.StoneShade, l.StoneTop };
                    for (int n = 1; n <= bands; n++)
                    {
                        rim.Add(new Vector2(.64f - slim * n / bands, .22f + band * n));
                        paint.Add((bands - n) % 2 == 0 ? red : white);
                    }
                    float waist = .64f - slim;
                    rim.Add(new Vector2(waist + .16f, top));            paint.Add(l.StoneShade);
                    rim.Add(new Vector2(waist + .16f, top + .12f));     paint.Add(gold);
                    rim.Add(new Vector2(waist - .2f, top + .12f));      paint.Add(l.StoneTop);
                    rim.Add(new Vector2(waist - .2f, top + .6f));       paint.Add(lamp);
                    rim.Add(new Vector2(waist - .1f, top + .6f));       paint.Add(cap);
                    rim.Add(new Vector2(waist - .28f, top + .82f));     paint.Add(cap);
                    rim.Add(new Vector2(.03f, top + 1.04f));            paint.Add(cap);
                    Turned(kit, corner, rim.ToArray(), paint.ToArray(), 16);
                    kit.Ball(corner + Vector3.up * (top + 1.08f), .075f, gold, 8);
                    // The lamp itself flickers inside its glass.
                    AddFlame(root, corner + Vector3.up * (top + .26f), far ? 1.15f : 1.05f, 1);
                    break;
                }
                case Look.Ember:
                {
                    // Dark stone under a golden crown, and a fire burning in the crown. The far pair stand taller.
                    float lift = far ? .62f : 0f;
                    Turned(kit, corner, new[]
                    {
                        new Vector2(.72f, 0f), new Vector2(.70f, .2f), new Vector2(.64f, .2f), new Vector2(.62f, .98f + lift), new Vector2(.66f, .98f + lift),
                        new Vector2(.66f, 1.1f + lift), new Vector2(.62f, 1.1f + lift), new Vector2(.62f, 1.22f + lift), new Vector2(.70f, 1.22f + lift),
                        new Vector2(.70f, 1.42f + lift), new Vector2(.54f, 1.42f + lift), new Vector2(.5f, 1.3f + lift), new Vector2(.02f, 1.3f + lift)
                    }, new[] { l.Plinth, l.PlinthTop, l.Stone, goldDeep, gold, gold, l.Stone, goldDeep, gold, goldDeep, goldDeep, C(0xff8a1c, 1f) }, 16);
                    if (far)
                    {
                        // A seam of fire round the taller towers, half-way up.
                        Turned(kit, corner + Vector3.up * .78f, new[] { new Vector2(.635f, 0f), new Vector2(.645f, .03f), new Vector2(.635f, .06f) },
                            new[] { C(0xff8a1c, 1f), C(0xffc84a, 1f) }, 16);
                    }
                    for (int m = 0; m < 8; m++)
                    {
                        float a = (m + .5f) * Mathf.PI / 4f;
                        var foot = corner + new Vector3(Mathf.Cos(a) * .62f, 1.4f + lift, Mathf.Sin(a) * .62f);
                        kit.Spike(foot, foot + new Vector3(Mathf.Cos(a) * .05f, m % 2 == 0 ? .36f : .26f, Mathf.Sin(a) * .05f), .14f, gold, C(0xffdf7a, .9f), 4);
                        if (m % 2 == 0) kit.Ball(foot + new Vector3(Mathf.Cos(a) * .06f, .4f, Mathf.Sin(a) * .06f), .055f, C(0xe2334a, 1f), 6);
                    }
                    AddFlame(root, corner + Vector3.up * (1.36f + lift), far ? 1.5f : 1.2f);
                    break;
                }
                case Look.Pharaoh:
                {
                    // A stepped pyramid with a golden capstone. The far pair stand taller, each with a palm growing behind it.
                    float[] broad = far ? new[] { 1.04f, .84f, .64f, .46f } : new[] { .9f, .68f, .48f };
                    float rise = far ? .4f : .22f, foot = far ? 1.24f : 1.1f, at = WallHigh + .08f;
                    kit.Box(corner + Vector3.up * (at * .5f), new Vector3(foot, at, foot), Quaternion.identity, l.StoneShade, l.Stone);
                    for (int n = 0; n < broad.Length; n++)
                    {
                        kit.Box(corner + Vector3.up * (at + rise * .5f), new Vector3(broad[n], rise, broad[n]), Quaternion.identity,
                            n % 2 == 0 ? l.Stone : l.StoneShade, l.StoneTop);
                        at += rise;
                    }
                    float tip = broad[broad.Length - 1] - .08f;
                    kit.Box(corner + Vector3.up * (at + .025f), new Vector3(tip + .06f, .05f, tip + .06f), Quaternion.identity, C(0x1fb5a8, .6f), C(0x1fb5a8, .6f));
                    kit.Spike(corner + Vector3.up * (at + .05f), corner + Vector3.up * (at + .05f + tip * 1.1f), tip * .7071f, gold, C(0xffdf7a, .9f), 4);
                    if (far) Palm(kit, corner + new Vector3(-Mathf.Sign(corner.x) * .55f, 0f, .8f), 2.5f, .8f);
                    break;
                }
                case Look.Jade:
                {
                    // A stepped pillar of mossy stone with a band of gold, a cut jade on top and leaves round it.
                    // The far pair stand taller.
                    float lift = far ? .6f : 0f;
                    Color32 jade = C(0x2fc48a, .9f), jadeLight = C(0x8af0c4, .9f), frond = C(0x3f9a3a, .2f), frondLight = C(0x62bf4a, .2f);
                    Turned(kit, corner, new[]
                    {
                        new Vector2(.74f, 0f), new Vector2(.72f, .26f), new Vector2(.62f, .26f), new Vector2(.60f, .7f + lift), new Vector2(.52f, .7f + lift),
                        new Vector2(.50f, 1.02f + lift), new Vector2(.56f, 1.02f + lift), new Vector2(.56f, 1.14f + lift), new Vector2(.44f, 1.14f + lift),
                        new Vector2(.42f, 1.3f + lift), new Vector2(.02f, 1.3f + lift)
                    }, new[] { l.Plinth, l.PlinthTop, l.Stone, l.StoneTop, l.StoneShade, goldDeep, gold, gold, l.Stone, l.StoneTop }, 8);
                    kit.Gem(corner + Vector3.up * (1.3f + lift), Quaternion.identity, new Vector3(.3f, .44f, .3f), jade, jadeLight, 6);
                    for (int m = 0; m < 6; m++)
                    {
                        float a = m * Mathf.PI / 3f + .4f;
                        var foot = corner + new Vector3(Mathf.Cos(a) * .34f, 1.3f + lift, Mathf.Sin(a) * .34f);
                        kit.Spike(foot, foot + new Vector3(Mathf.Cos(a) * .44f, m % 2 == 0 ? .16f : .32f, Mathf.Sin(a) * .44f), .14f, frond, frondLight, 4);
                    }
                    break;
                }
                case Look.Gridiron:
                {
                    // A floodlight: a steel mast with a bank of lamps turned on the field. The far pair stand taller.
                    // The goal posts stand on the end walls, and are built with the towers at their west ends.
                    float tall = far ? 3.1f : 1.5f;
                    Color32 steel = C(0xb9c2d0, .6f), steelDeep = C(0x7c8698, .5f), lamp = C(0xfffbe0, 1f);
                    kit.Box(corner + Vector3.up * .2f, new Vector3(1.2f, .4f, 1.2f), Quaternion.identity, l.Plinth, l.PlinthTop);
                    kit.Capsule(corner + Vector3.up * .4f, corner + Vector3.up * tall, .13f, .09f, steel, 8);
                    Vector3 inward = new Vector3(-Mathf.Sign(corner.x), 0f, -Mathf.Sign(corner.z)).normalized;
                    Quaternion face = Quaternion.LookRotation(inward, Vector3.up) * Quaternion.Euler(28f, 0f, 0f);
                    Vector3 head = corner + Vector3.up * (tall + .2f) + inward * .12f;
                    kit.Box(head, new Vector3(1f, .56f, .16f), face, steelDeep, steel);
                    for (int row = 0; row < 2; row++)
                    {
                        for (int column = 0; column < 3; column++)
                            kit.Ball(head + face * new Vector3((column - 1) * .3f, (row - .5f) * .26f, .09f), .1f, lamp, 6);
                    }
                    AddFlame(root, head + face * new Vector3(0f, -.1f, .16f), far ? 1.2f : .95f, 1);
                    if (corner.x < 0f) GoalPost(kit, new Vector3(0f, 0f, corner.z), far ? 2.6f : .9f);
                    break;
                }
                default:
                {
                    // Blue ice with a band of gold, a brim heaped with snow, and a blue spire over it. The far
                    // pair stand taller, under longer spires.
                    Color32 roof = C(0x2f55c8, .35f), roofLight = C(0x4a78e6, .35f), snow = C(0xffffff, .2f);
                    float spire = far ? 1.3f : .68f, lift = far ? .5f : 0f;
                    Turned(kit, corner, new[]
                    {
                        new Vector2(.72f, 0f), new Vector2(.70f, .2f), new Vector2(.64f, .2f), new Vector2(.61f, 1.02f + lift), new Vector2(.65f, 1.02f + lift),
                        new Vector2(.65f, 1.13f + lift), new Vector2(.61f, 1.13f + lift), new Vector2(.61f, 1.2f + lift), new Vector2(.78f, 1.2f + lift),
                        new Vector2(.80f, 1.3f + lift), new Vector2(.70f, 1.38f + lift), new Vector2(.62f, 1.38f + lift),
                        new Vector2(.36f, 1.38f + lift + .5f * spire), new Vector2(.03f, 1.38f + lift + 1.05f * spire)
                    }, new[] { l.Plinth, l.PlinthTop, l.Stone, goldDeep, gold, gold, l.Stone, l.StoneShade, snow, snow, snow, roof, roofLight }, 16);
                    kit.Ball(corner + Vector3.up * (1.42f + lift + 1.05f * spire), .085f, gold, 8);
                    for (int m = 0; m < 5; m++)
                    {
                        float a = m * 1.26f + corner.x + corner.z;
                        kit.Blob(corner + new Vector3(Mathf.Cos(a) * .7f, 1.33f + lift, Mathf.Sin(a) * .7f), new Vector3(.2f, .12f, .2f), Quaternion.identity, snow, 1f, 1f, 8);
                    }
                    kit.Blob(corner + new Vector3(.12f, 1.38f + lift + .62f * spire, -.16f), new Vector3(.2f, .08f, .16f), Quaternion.identity, snow, 1f, 1f, 8);
                    break;
                }
            }
        }

        /// <summary>The post that stands on each side wall at the half-way line, in the look of the map.</summary>
        void BuildPost(MeshKit kit, Transform root, Vector3 post)
        {
            Look l = look;
            kit.Blob(post + Vector3.up * .5f, new Vector3(.46f, .52f, .46f), Quaternion.identity, l.Stone, .35f, .5f, 10);
            kit.Blob(post + Vector3.up * 1.05f, new Vector3(.5f, .07f, .5f), Quaternion.identity, l.Index == Look.Frost ? C(0xffffff, .2f) : l.Trim, .5f, .5f, 10);
            switch (l.Index)
            {
                case Look.Tidal:
                    // A harbour lantern.
                    kit.Capsule(post + Vector3.up * 1.08f, post + Vector3.up * 1.2f, .16f, .14f, C(Kit.GoldDark, .8f), 8);
                    AddFlame(root, post + Vector3.up * 1.2f, .8f, 1);
                    kit.Blob(post + Vector3.up * 1.56f, new Vector3(.2f, .05f, .2f), Quaternion.identity, l.Trim, 1f, 1f, 8);
                    break;
                case Look.Ember:
                    kit.Blob(post + Vector3.up * 1.14f, new Vector3(.3f, .1f, .3f), Quaternion.identity, l.Bowl, .8f, 1f, 10);
                    AddFlame(root, post + Vector3.up * 1.2f, 1f);
                    break;
                case Look.Pharaoh:
                    // A bronze bowl of fire on a capital of turquoise.
                    kit.Blob(post + Vector3.up * 1.12f, new Vector3(.26f, .1f, .26f), Quaternion.identity, C(0x1fb5a8, .5f), .6f, 1f, 10);
                    kit.Blob(post + Vector3.up * 1.22f, new Vector3(.32f, .1f, .32f), Quaternion.identity, C(0x8a5a2a, .7f), .8f, 1f, 10);
                    AddFlame(root, post + Vector3.up * 1.28f, 1f);
                    break;
                case Look.Jade:
                {
                    // A cut jade standing in a ring of leaves.
                    Color32 frond = C(0x3f9a3a, .2f), frondLight = C(0x62bf4a, .2f);
                    kit.Gem(post + Vector3.up * 1.1f, Quaternion.identity, new Vector3(.24f, .38f, .24f), C(0x2fc48a, .9f), C(0x8af0c4, .9f), 6);
                    for (int m = 0; m < 5; m++)
                    {
                        float a = m * Mathf.PI * .4f + post.x;
                        var from = post + new Vector3(Mathf.Cos(a) * .26f, 1.1f, Mathf.Sin(a) * .26f);
                        kit.Spike(from, from + new Vector3(Mathf.Cos(a) * .36f, m % 2 == 0 ? .12f : .24f, Mathf.Sin(a) * .36f), .12f, frond, frondLight, 4);
                    }
                    break;
                }
                case Look.Gridiron:
                    // A football standing on its tee.
                    kit.Capsule(post + Vector3.up * 1.08f, post + Vector3.up * 1.2f, .11f, .06f, C(0xff7a1a, .5f), 8);
                    kit.Blob(post + Vector3.up * 1.46f, new Vector3(.17f, .28f, .17f), Quaternion.identity, C(0x9a5a2c, .4f), 1f, 1f, 10);
                    kit.Blob(post + Vector3.up * 1.61f, new Vector3(.125f, .035f, .125f), Quaternion.identity, C(0xffffff, .4f), 1f, 1f, 10);
                    kit.Blob(post + Vector3.up * 1.31f, new Vector3(.125f, .035f, .125f), Quaternion.identity, C(0xffffff, .4f), 1f, 1f, 10);
                    break;
                default:
                {
                    // A cluster of ice.
                    Color32 ice = C(0x7fdcff, .9f), iceLight = C(0xd4f6ff, .9f);
                    kit.Spike(post + Vector3.up * 1.05f, post + new Vector3(0f, 1.85f, 0f), .17f, ice, iceLight, 5);
                    kit.Spike(post + new Vector3(.14f, 1.05f, .1f), post + new Vector3(.3f, 1.5f, .2f), .11f, ice, iceLight, 5);
                    kit.Spike(post + new Vector3(-.13f, 1.05f, -.08f), post + new Vector3(-.28f, 1.42f, -.2f), .1f, ice, iceLight, 5);
                    break;
                }
            }
        }

        /// <summary>One battlement, in the look of the map.</summary>
        static void Merlon(MeshKit kit, Vector3 at, Vector3 size, int number)
        {
            Look l = look;
            switch (l.Index)
            {
                case Look.Tidal:
                    // Pale blocks, every other one a gilded bollard.
                    kit.Blob(at, size, Quaternion.identity, number % 2 == 0 ? l.Block : l.BlockDark, .45f, .45f, 8);
                    if (number % 2 != 0) kit.Ball(at + Vector3.up * .16f, .08f, l.Trim, 6);
                    break;
                case Look.Ember:
                    // The points of a crown, set in dark stone.
                    kit.Blob(at, size, Quaternion.identity, number % 2 == 0 ? l.Block : l.BlockDark, .45f, .45f, 8);
                    if (number % 2 == 0) kit.Spike(at + Vector3.up * .1f, at + Vector3.up * .36f, .12f, l.Trim, C(0xffdf7a, .9f), 4);
                    break;
                case Look.Frost:
                    // Heaps of snow between blocks of clear ice.
                    if (number % 2 == 0) kit.Blob(at + Vector3.up * .01f, new Vector3(.25f, .15f, .25f), Quaternion.identity, l.Block, 1f, 1f, 8);
                    else kit.Blob(at, size, Quaternion.identity, l.BlockDark, .45f, .45f, 8);
                    break;
                case Look.Pharaoh:
                    // Sandstone blocks, every other one a little golden pyramid.
                    if (number % 2 == 0) kit.Blob(at, size, Quaternion.identity, l.Block, .45f, .45f, 8);
                    else kit.Spike(at - Vector3.up * .06f, at + Vector3.up * .26f, .17f, l.Trim, C(0xffdf7a, .9f), 4);
                    break;
                case Look.Jade:
                    // Mossy blocks, with a tuft of leaves growing between them and here and there a flower.
                    if (number % 2 == 0) kit.Blob(at, size, Quaternion.identity, l.Block, .45f, .45f, 8);
                    else
                    {
                        kit.Blob(at + Vector3.up * .01f, new Vector3(.22f, .13f, .2f), Quaternion.identity, C(0x4fae4a, .15f), 1f, 1f, 8);
                        if (number % 4 == 1) kit.Ball(at + new Vector3(.04f, .13f, .02f), .055f, C(0xff9a2e, .4f), 6);
                    }
                    break;
                case Look.Gridiron:
                    // Pads along the top of the wall, every other one an orange pylon.
                    if (number % 2 == 0) kit.Blob(at, size, Quaternion.identity, l.Block, .45f, .45f, 8);
                    else kit.Spike(at - Vector3.up * .06f, at + Vector3.up * .3f, .11f, C(0xff7a1a, .4f), C(0xffa040, .4f), 4);
                    break;
                default:
                    kit.Blob(at, size, Quaternion.identity, number % 2 == 0 ? l.Block : l.BlockDark, .45f, .45f, 8);
                    break;
            }
        }

        /// <summary>What marks a team's half along the top of the walls: a shield, or in the harbour a lifebuoy in the team's colour.</summary>
        static void Shield(MeshKit kit, Vector3 spot, Vector3 lean, int team)
        {
            Look l = look;
            Quaternion tilt = Quaternion.Euler(lean);
            if (l.Index != Look.Tidal)
            {
                kit.Blob(spot, new Vector3(.27f, .05f, .27f), tilt, C(Kit.TeamMain[team], .6f), 1f, 1f, 12);
                kit.Blob(spot + tilt * Vector3.up * .035f, new Vector3(.1f, .05f, .1f), tilt, l.Trim, 1f, 1f, 8);
                return;
            }
            Color32 main = C(Kit.TeamMain[team], .5f), pale = C(0xfffaf0, .4f);
            for (int i = 0; i < 8; i++)
            {
                float a0 = (i - .5f) * Mathf.PI / 4f, a1 = (i + .5f) * Mathf.PI / 4f;
                kit.Capsule(spot + tilt * (new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * .2f), spot + tilt * (new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * .2f),
                    .075f, .075f, i % 2 == 0 ? main : pale, 6);
            }
        }

        // ------------------------------------------------------------------ what stands by the royal box

        /// <summary>What the map sets out on the terrace of the royal box, either side of the emperor.</summary>
        void BuildBoxOrnaments(MeshKit kit, Transform root, float z)
        {
            Look l = look;
            Color32 gold = l.Trim, goldDeep = C(Kit.GoldDark, .8f);
            for (int side = -1; side <= 1; side += 2)
            {
                var foot = new Vector3(side * 2.02f, 1.7f, z - .98f);
                switch (l.Index)
                {
                    case Look.Tidal:
                    {
                        // A cask and a crate off the quay, and a lifebuoy hung on the front of the box.
                        Color32 wood = C(0xb9763f, .1f), woodDeep = C(0x8a5230, .1f), hoop = C(0x5a4a44, .5f);
                        Turned(kit, foot, new[]
                        {
                            new Vector2(.15f, 0f), new Vector2(.2f, .14f), new Vector2(.2f, .19f), new Vector2(.21f, .3f), new Vector2(.2f, .41f),
                            new Vector2(.2f, .46f), new Vector2(.15f, .6f), new Vector2(.02f, .6f)
                        }, new[] { woodDeep, hoop, wood, wood, hoop, woodDeep, wood }, 10);
                        kit.Box(foot + new Vector3(-side * .42f, .17f, .16f), new Vector3(.34f, .34f, .34f), Quaternion.Euler(0f, 18f * side, 0f), woodDeep, wood);
                        Vector3 buoy = new Vector3(side * 1.42f, 1.12f, z - 1.27f);
                        for (int i = 0; i < 8; i++)
                        {
                            float a0 = (i - .5f) * Mathf.PI / 4f, a1 = (i + .5f) * Mathf.PI / 4f;
                            kit.Capsule(buoy + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * .24f, buoy + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * .24f,
                                .08f, .08f, i % 2 == 0 ? C(0xe2493a, .4f) : C(0xfffaf0, .4f), 6);
                        }
                        break;
                    }
                    case Look.Ember:
                        // A golden bowl of fire.
                        kit.Capsule(foot, foot + Vector3.up * .34f, .1f, .07f, goldDeep, 8);
                        kit.Blob(foot + Vector3.up * .44f, new Vector3(.3f, .13f, .3f), Quaternion.identity, gold, .8f, 1f, 12);
                        kit.Blob(foot + Vector3.up * .5f, new Vector3(.23f, .06f, .23f), Quaternion.identity, C(0xff8a1c, 1f), 1f, 1f, 10);
                        AddFlame(root, foot + Vector3.up * .5f, 1.1f);
                        break;
                    case Look.Pharaoh:
                        // A pyramid capped with gold, and a palm beside it.
                        kit.Spike(foot, foot + Vector3.up * .74f, .5f, l.Stone, l.StoneTop, 4);
                        kit.Spike(foot + Vector3.up * .5f, foot + Vector3.up * .78f, .18f, gold, C(0xffdf7a, .9f), 4);
                        Palm(kit, foot + new Vector3(-side * .52f, 0f, .12f), 1.35f, .62f);
                        break;
                    case Look.Jade:
                    {
                        // A stone idol holding up a cut jade, with broad leaves at its foot.
                        Color32 frond = C(0x3f9a3a, .2f), frondLight = C(0x62bf4a, .2f);
                        Turned(kit, foot, new[]
                        {
                            new Vector2(.26f, 0f), new Vector2(.24f, .16f), new Vector2(.18f, .16f), new Vector2(.17f, .5f), new Vector2(.23f, .5f),
                            new Vector2(.23f, .6f), new Vector2(.02f, .6f)
                        }, new[] { l.StoneShade, l.StoneTop, l.Stone, goldDeep, gold, l.StoneTop }, 8);
                        kit.Gem(foot + Vector3.up * .6f, Quaternion.identity, new Vector3(.2f, .32f, .2f), C(0x2fc48a, .9f), C(0x8af0c4, .9f), 6);
                        for (int m = 0; m < 5; m++)
                        {
                            float a = m * 1.26f + side;
                            var from = foot + new Vector3(Mathf.Cos(a) * .2f, .02f, Mathf.Sin(a) * .2f);
                            kit.Spike(from, from + new Vector3(Mathf.Cos(a) * .42f, .26f, Mathf.Sin(a) * .42f), .12f, frond, frondLight, 4);
                        }
                        break;
                    }
                    case Look.Gridiron:
                    {
                        // A cup for the winners, and a football beside it.
                        Turned(kit, foot, new[]
                        {
                            new Vector2(.2f, 0f), new Vector2(.2f, .08f), new Vector2(.07f, .12f), new Vector2(.06f, .3f), new Vector2(.14f, .36f),
                            new Vector2(.27f, .66f), new Vector2(.24f, .66f), new Vector2(.02f, .5f)
                        }, new[] { goldDeep, gold, goldDeep, gold, gold, C(0xffdf7a, .9f), goldDeep }, 12);
                        for (int hand = -1; hand <= 1; hand += 2)
                        {
                            kit.Capsule(foot + new Vector3(hand * .2f, .46f, 0f), foot + new Vector3(hand * .35f, .54f, 0f), .035f, .035f, gold, 6);
                            kit.Capsule(foot + new Vector3(hand * .35f, .54f, 0f), foot + new Vector3(hand * .29f, .64f, 0f), .035f, .035f, gold, 6);
                        }
                        kit.Blob(foot + new Vector3(-side * .44f, .12f, .14f), new Vector3(.2f, .12f, .12f), Quaternion.Euler(0f, 30f * side, 0f), C(0x9a5a2c, .4f), 1f, 1f, 10);
                        break;
                    }
                    case Look.Frost:
                    {
                        // A cluster of tall ice standing in a heap of snow.
                        Color32 ice = C(0x7fdcff, .9f), iceLight = C(0xd4f6ff, .9f), iceDeep = C(0x48aef0, .9f);
                        kit.Spike(foot, foot + new Vector3(side * .06f, 1.5f, 0f), .26f, ice, iceLight, 5);
                        kit.Spike(foot + new Vector3(-side * .2f, 0f, .06f), foot + new Vector3(-side * .42f, 1f, .1f), .18f, iceDeep, ice, 5);
                        kit.Spike(foot + new Vector3(side * .16f, 0f, -.12f), foot + new Vector3(side * .34f, .8f, -.22f), .16f, ice, iceLight, 5);
                        kit.Blob(foot + new Vector3(0f, .05f, 0f), new Vector3(.42f, .12f, .3f), Quaternion.identity, C(0xffffff, .2f), 1f, 1f, 10);
                        break;
                    }
                }
            }
        }
    }
}
