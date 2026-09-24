"""Build every EXFIL asset pack: characters, weapons, props -> Assets/Art/Models."""
import json
import os
import shutil
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bake_props as bp
import bake_characters as bc
import pack_io as pk

DL = '/tmp/dl'
OUT = '/tmp/out'
PROJECT = '/home/user/2D'
DEST = os.path.join(PROJECT, 'Assets/Art/Models')

KK = DL + '/KayKit-Character-Pack-Adventures-1.0/addons/kaykit_character_pack_adventures'
KK_CHARS = KK + '/Characters/gltf'
KK_WEAPONS = KK + '/Assets/gltf'
KK_WEAPON_TEX = KK + '/Assets/fbx'
SB = DL + '/KayKit-Space-Base-Bits-1.0/addons/kaykit_space_base_bits/Assets'
PB = DL + '/KayKit-Prototype-Bits-1.0/addons/kaykit_prototype_bits/Assets'
DG = DL + '/KayKit-Dungeon-Remastered-1.0/addons/kaykit_dungeon_remastered/Assets'
HEX = DL + '/KayKit-Hexagons'
KA = DL + '/Starter-Kit-Basic-Scene/sample/Mini Arena/Models/GLB format'
KC = DL + '/CityBuilder/models'
KF = DL + '/Starter-Kit-FPS/models'
KP = DL + '/Starter-Kit-3D-Platformer/models'

GROUPS = {
    'sb': dict(tex=SB + '/obj/spacebits_texture.png', texname='spacebits_texture.png',
               source='KayKit Space Base Bits (CC0, Kay Lousberg)'),
    'pb': dict(tex=PB + '/obj/prototypebits_texture.png', texname='prototypebits_texture.png',
               source='KayKit Prototype Bits (CC0, Kay Lousberg)'),
    'dg': dict(tex=DG + '/obj/dungeon_texture.png', texname='dungeon_texture.png',
               source='KayKit Dungeon Remastered (CC0, Kay Lousberg)'),
    'ka': dict(tex=KA + '/Textures/colormap.png', texname='kenney_arena_texture.png',
               source='Kenney Mini Arena (CC0, Kenney)'),
    'kc': dict(tex=KC + '/Textures/colormap.png', texname='kenney_city_texture.png',
               source='Kenney City Builder (CC0, Kenney)'),
    'kf': dict(tex=KF + '/Textures/colormap.png', texname='kenney_fps_texture.png',
               source='Kenney Starter Kit FPS (CC0, Kenney)'),
    'kp': dict(tex=KP + '/Textures/colormap.png', texname='kenney_platform_texture.png',
               source='Kenney Starter Kit 3D Platformer (CC0, Kenney)'),
    'hex': dict(tex=HEX + '/Models/Items/detail_forestA.gltf.glb', texname='',
                source='KayKit Hexagons (CC0, Kay Lousberg)'),
}

# ------------------------------------------------------------------ catalogues
# characters: key, glb, texture, display name
CHARACTERS = [
    ('Operator_Assault', KK_CHARS + '/Knight.glb', KK_CHARS + '/knight_texture.png', 'Assault'),
    ('Operator_Heavy', KK_CHARS + '/Barbarian.glb', KK_CHARS + '/barbarian_texture.png', 'Heavy'),
    ('Operator_Scout', KK_CHARS + '/Rogue.glb', KK_CHARS + '/rogue_texture.png', 'Scout'),
    ('Operator_Ghost', KK_CHARS + '/Rogue_Hooded.glb', KK_CHARS + '/rogue_texture.png', 'Ghost'),
    ('Operator_Tech', KK_CHARS + '/Mage.glb', KK_CHARS + '/mage_texture.png', 'Tech'),
    ('Operator_Recruit', KA + '/character-soldier.glb', KA + '/Textures/colormap.png', 'Recruit'),
]

# weapons: key, glb/obj, kind
WEAPONS = [
    ('wpn_pistol', KF + '/blaster.glb', 'pistol'),
    ('wpn_smg', KF + '/blaster.glb', 'smg'),
    ('wpn_rifle', KF + '/blaster-repeater.glb', 'rifle'),
    ('wpn_ak', KF + '/blaster-repeater.glb', 'rifle'),
    ('wpn_dmr', KK_WEAPONS + '/crossbow_2handed.gltf', 'dmr'),
    ('wpn_sniper', KK_WEAPONS + '/crossbow_2handed.gltf', 'sniper'),
    ('wpn_shotgun', KF + '/blaster-repeater.glb', 'shotgun'),
    ('wpn_melee_knife', KK_WEAPONS + '/dagger.gltf', 'melee'),
    ('wpn_melee_axe', KK_WEAPONS + '/axe_1handed.gltf', 'melee'),
    ('wpn_melee_sword', KK_WEAPONS + '/sword_1handed.gltf', 'melee'),
    ('wpn_launcher', KF + '/blaster-repeater.glb', 'launcher'),
]

# props: (group, key, file, tags)
PROPS = [
    # -- space base: raid site + bunker modules -------------------------------
    ('sb', 'sb_basemodule_a', 'obj/basemodule_A.obj', ['module', 'building', 'room']),
    ('sb', 'sb_basemodule_b', 'obj/basemodule_B.obj', ['module', 'building', 'room']),
    ('sb', 'sb_basemodule_c', 'obj/basemodule_C.obj', ['module', 'building', 'room']),
    ('sb', 'sb_basemodule_garage', 'obj/basemodule_garage.obj', ['module', 'building', 'garage']),
    ('sb', 'sb_roofmodule_base', 'obj/roofmodule_base.obj', ['roof', 'module']),
    ('sb', 'sb_roofmodule_cargo_a', 'obj/roofmodule_cargo_A.obj', ['roof', 'module', 'cargo']),
    ('sb', 'sb_cargo_a', 'obj/cargo_A.obj', ['crate', 'cargo', 'loot']),
    ('sb', 'sb_cargo_b', 'obj/cargo_B.obj', ['crate', 'cargo', 'loot']),
    ('sb', 'sb_cargo_a_stacked', 'obj/cargo_A_stacked.obj', ['crate', 'cargo', 'stack']),
    ('sb', 'sb_cargo_a_packed', 'obj/cargo_A_packed.obj', ['crate', 'cargo', 'loot']),
    ('sb', 'sb_cargodepot_a', 'obj/cargodepot_A.obj', ['crate', 'cargo', 'depot']),
    ('sb', 'sb_cargodepot_b', 'obj/cargodepot_B.obj', ['crate', 'cargo', 'depot']),
    ('sb', 'sb_containers_a', 'obj/containers_A.obj', ['container', 'crate', 'loot']),
    ('sb', 'sb_containers_b', 'obj/containers_B.obj', ['container', 'crate', 'loot']),
    ('sb', 'sb_containers_c', 'obj/containers_C.obj', ['container', 'crate', 'loot']),
    ('sb', 'sb_containers_d', 'obj/containers_D.obj', ['container', 'crate', 'loot']),
    ('sb', 'sb_structure_low', 'obj/structure_low.obj', ['structure', 'wall', 'building']),
    ('sb', 'sb_structure_tall', 'obj/structure_tall.obj', ['structure', 'wall', 'building']),
    ('sb', 'sb_tunnel_straight_a', 'obj/tunnel_straight_A.obj', ['tunnel', 'wall', 'corridor']),
    ('sb', 'sb_tunnel_diagonal_long_a', 'obj/tunnel_diagonal_long_A.obj', ['tunnel', 'wall', 'corridor']),
    ('sb', 'sb_terrain_low', 'obj/terrain_low.obj', ['terrain', 'ground']),
    ('sb', 'sb_terrain_mining', 'obj/terrain_mining.obj', ['terrain', 'ground']),
    ('sb', 'sb_rock_a', 'obj/rock_A.obj', ['rock', 'nature']),
    ('sb', 'sb_rocks_a', 'obj/rocks_A.obj', ['rock', 'nature']),
    ('sb', 'sb_solarpanel', 'obj/solarpanel.obj', ['panel', 'prop']),
    ('sb', 'sb_lights', 'obj/lights.obj', ['light', 'prop']),
    ('sb', 'sb_drill_structure', 'obj/drill_structure.obj', ['structure', 'machine', 'prop']),
    ('sb', 'sb_landingpad_small', 'obj/landingpad_small.obj', ['pad', 'ground']),
    ('pb', 'pb_wall_target', 'obj/Wall_Target.obj', ['target', 'wall', 'range']),
    ('pb', 'pb_target_small', 'obj/target_small.obj', ['target', 'range', 'small']),
    ('pb', 'pb_target_stand_a', 'obj/target_stand_A.obj', ['target', 'range', 'stand']),
    ('pb', 'pb_wall_decorated', 'obj/Wall_Decorated.obj', ['wall', 'interior', 'decorated']),
    ('pb', 'pb_primitive_wall', 'obj/Primitive_Wall.obj', ['wall', 'primitive']),
    ('pb', 'pb_primitive_doorway', 'obj/Primitive_Doorway.obj', ['doorway', 'primitive']),
    ('pb', 'pb_primitive_window', 'obj/Primitive_Window.obj', ['window', 'primitive']),
    ('pb', 'pb_primitive_cube', 'obj/Primitive_Cube.obj', ['block', 'primitive']),
    ('dg', 'dg_shelf_small_candles', 'obj/shelf_small_candles.obj', ['shelf', 'light', 'furniture']),
    ('dg', 'dg_torch_mounted', 'obj/torch_mounted.obj', ['light', 'wall']),
    ('dg', 'dg_bed_floor', 'obj/bed_floor.obj', ['bed', 'furniture']),
    ('dg', 'dg_wall_shelves', 'obj/wall_shelves.obj', ['shelf', 'wall', 'storage']),
    ('dg', 'dg_box_stacked', 'obj/box_stacked.obj', ['crate', 'box', 'stack']),
    ('ka', 'ka_weapon_sword', 'weapon-sword.glb', ['melee', 'prop']),
    ('sb', 'sb_landingpad_large', 'obj/landingpad_large.obj', ['pad', 'ground']),
    ('sb', 'sb_spacetruck', 'obj/spacetruck.obj', ['vehicle', 'prop', 'cover']),
    ('sb', 'sb_spacetruck_large', 'obj/spacetruck_large.obj', ['vehicle', 'prop', 'cover']),
    ('sb', 'sb_basemodule_d', 'obj/basemodule_D.obj', ['module', 'building', 'room']),
    ('sb', 'sb_basemodule_e', 'obj/basemodule_E.obj', ['module', 'building', 'room']),
    ('sb', 'sb_roofmodule_cargo_b', 'obj/roofmodule_cargo_B.obj', ['roof', 'module', 'cargo']),
    # -- prototype bits: bunker kit ------------------------------------------
    ('pb', 'pb_wall', 'obj/Wall.obj', ['wall', 'interior']),
    ('pb', 'pb_wall_half', 'obj/Wall_Half.obj', ['wall', 'interior']),
    ('pb', 'pb_wall_doorway', 'obj/Wall_Doorway.obj', ['wall', 'door', 'interior']),
    ('pb', 'pb_wall_window_closed', 'obj/Wall_Window_Closed.obj', ['wall', 'window', 'interior']),
    ('pb', 'pb_wall_window_open', 'obj/Wall_Window_Open.obj', ['wall', 'window', 'interior']),
    ('pb', 'pb_floor', 'obj/Floor.obj', ['floor', 'interior']),
    ('pb', 'pb_floor_prototype', 'obj/Floor_Prototype.obj', ['floor', 'interior']),
    ('pb', 'pb_door_a', 'obj/Door_A.obj', ['door', 'interior']),
    ('pb', 'pb_door_b', 'obj/Door_B.obj', ['door', 'interior']),
    ('pb', 'pb_pillar_a', 'obj/Pillar_A.obj', ['pillar', 'interior']),
    ('pb', 'pb_pillar_b', 'obj/Pillar_B.obj', ['pillar', 'interior']),
    ('pb', 'pb_beam', 'obj/Primitive_Beam.obj', ['beam', 'interior']),
    ('pb', 'pb_cube', 'obj/Primitive_Cube.obj', ['block', 'interior']),
    ('pb', 'pb_stairs', 'obj/Primitive_Stairs.obj', ['stairs', 'interior']),
    ('pb', 'pb_slope', 'obj/Primitive_Slope.obj', ['ramp', 'interior']),
    ('pb', 'pb_box_a', 'obj/Box_A.obj', ['crate', 'box', 'loot']),
    ('pb', 'pb_box_b', 'obj/Box_B.obj', ['crate', 'box', 'loot']),
    ('pb', 'pb_box_c', 'obj/Box_C.obj', ['crate', 'box', 'loot']),
    ('pb', 'pb_barrel_a', 'obj/Barrel_A.obj', ['barrel', 'loot']),
    ('pb', 'pb_barrel_b', 'obj/Barrel_B.obj', ['barrel', 'loot']),
    ('pb', 'pb_barrel_c', 'obj/Barrel_C.obj', ['barrel', 'loot']),
    ('pb', 'pb_can_a', 'obj/Can_A.obj', ['can', 'loot', 'small']),
    ('pb', 'pb_pallet_large', 'obj/Pallet_Large.obj', ['pallet', 'prop']),
    ('pb', 'pb_pallet_small', 'obj/Pallet_Small.obj', ['pallet', 'prop']),
    ('pb', 'pb_table_medium', 'obj/table_medium.obj', ['table', 'furniture']),
    ('pb', 'pb_table_long', 'obj/table_medium_long.obj', ['table', 'furniture']),
    # -- dungeon: hideout furniture, loot, storage ---------------------------
    ('dg', 'dg_chest', 'obj/chest.obj', ['chest', 'loot', 'storage']),
    ('dg', 'dg_chest_gold', 'obj/chest_gold.obj', ['chest', 'loot', 'rare']),
    ('dg', 'dg_box_small', 'obj/box_small.obj', ['crate', 'box', 'loot']),
    ('dg', 'dg_box_large', 'obj/box_large.obj', ['crate', 'box', 'loot']),
    ('dg', 'dg_crates_stacked', 'obj/crates_stacked.obj', ['crate', 'stack', 'loot']),
    ('dg', 'dg_barrel_large', 'obj/barrel_large.obj', ['barrel', 'loot']),
    ('dg', 'dg_barrel_small', 'obj/barrel_small.obj', ['barrel', 'loot']),
    ('dg', 'dg_trunk_small', 'obj/trunk_small_A.obj', ['container', 'loot', 'storage']),
    ('dg', 'dg_trunk_medium', 'obj/trunk_medium_A.obj', ['container', 'loot', 'storage']),
    ('dg', 'dg_trunk_large', 'obj/trunk_large_A.obj', ['container', 'loot', 'storage']),
    ('dg', 'dg_shelf_large', 'obj/shelf_large.obj', ['shelf', 'storage', 'furniture']),
    ('dg', 'dg_shelf_small', 'obj/shelf_small.obj', ['shelf', 'storage', 'furniture']),
    ('dg', 'dg_bed_frame', 'obj/bed_frame.obj', ['bed', 'furniture', 'rest']),
    ('dg', 'dg_bed_decorated', 'obj/bed_decorated.obj', ['bed', 'furniture', 'rest']),
    ('dg', 'dg_table_small', 'obj/table_small.obj', ['table', 'furniture']),
    ('dg', 'dg_chair', 'obj/chair.obj', ['chair', 'furniture']),
    ('dg', 'dg_stool', 'obj/stool.obj', ['chair', 'furniture']),
    ('dg', 'dg_bottle_green', 'obj/bottle_A_green.obj', ['bottle', 'loot', 'small']),
    ('dg', 'dg_bottle_brown', 'obj/bottle_A_brown.obj', ['bottle', 'loot', 'small']),
    ('dg', 'dg_keg', 'obj/keg.obj', ['barrel', 'prop']),
    ('dg', 'dg_coin', 'obj/coin.obj', ['coin', 'valuable', 'small']),
    ('dg', 'dg_coin_stack', 'obj/coin_stack_small.obj', ['coin', 'valuable', 'small']),
    ('dg', 'dg_key', 'obj/key.obj', ['key', 'quest', 'small']),
    ('dg', 'dg_rubble', 'obj/rubble_large.obj', ['rubble', 'debris']),
    ('dg', 'dg_floor_tile', 'obj/floor_tile_large.obj', ['floor', 'interior']),
    ('dg', 'dg_floor_wood', 'obj/floor_wood_large.obj', ['floor', 'interior']),
    ('dg', 'dg_floor_dirt', 'obj/floor_dirt_large.obj', ['floor', 'ground']),
    ('dg', 'dg_floor_weeds', 'obj/floor_tile_small_weeds_A.obj', ['floor', 'garden', 'ground']),
    ('dg', 'dg_wall', 'obj/wall.obj', ['wall', 'interior']),
    ('dg', 'dg_wall_doorway', 'obj/wall_doorway.obj', ['wall', 'door', 'interior']),
    ('dg', 'dg_wall_window_open', 'obj/wall_window_open.obj', ['wall', 'window', 'interior']),
    ('dg', 'dg_wall_corner', 'obj/wall_corner.obj', ['wall', 'interior']),
    ('dg', 'dg_column', 'obj/column.obj', ['pillar', 'interior']),
    ('dg', 'dg_pillar', 'obj/pillar.obj', ['pillar', 'interior']),
    ('dg', 'dg_stairs', 'obj/stairs.obj', ['stairs', 'interior']),
    ('dg', 'dg_torch', 'obj/torch.obj', ['light', 'prop']),
    ('dg', 'dg_torch_lit', 'obj/torch_lit.obj', ['light', 'prop']),
    ('dg', 'dg_plate', 'obj/plate.obj', ['plate', 'furniture', 'small']),
    ('dg', 'dg_barrier', 'obj/barrier.obj', ['barrier', 'prop']),
    # -- kenney arena ---------------------------------------------------------
    ('ka', 'ka_wall', 'wall.glb', ['wall', 'arena']),
    ('ka', 'ka_wall_corner', 'wall-corner.glb', ['wall', 'arena']),
    ('ka', 'ka_wall_gate', 'wall-gate.glb', ['wall', 'gate', 'arena']),
    ('ka', 'ka_block', 'block.glb', ['block', 'arena']),
    ('ka', 'ka_bricks', 'bricks.glb', ['block', 'arena']),
    ('ka', 'ka_column', 'column.glb', ['pillar', 'arena']),
    ('ka', 'ka_column_damaged', 'column-damaged.glb', ['pillar', 'rubble', 'arena']),
    ('ka', 'ka_floor', 'floor.glb', ['floor', 'arena']),
    ('ka', 'ka_floor_detail', 'floor-detail.glb', ['floor', 'arena']),
    ('ka', 'ka_stairs', 'stairs.glb', ['stairs', 'arena']),
    ('ka', 'ka_trophy', 'trophy.glb', ['trophy', 'quest', 'prop']),
    ('ka', 'ka_weapon_rack', 'weapon-rack.glb', ['rack', 'hideout', 'storage']),
    ('ka', 'ka_weapon_spear', 'weapon-spear.glb', ['melee', 'prop']),
    ('ka', 'ka_banner', 'banner.glb', ['banner', 'decor']),
    ('ka', 'ka_statue', 'statue.glb', ['statue', 'decor']),
    ('ka', 'ka_tree', 'tree.glb', ['tree', 'nature']),
    ('ka', 'ka_border_straight', 'border-straight.glb', ['border', 'arena']),
    ('ka', 'ka_border_corner', 'border-corner.glb', ['border', 'arena']),
    # -- kenney city (raid exterior) -----------------------------------------
    ('kc', 'kc_building_a', 'building-small-a.glb', ['building', 'house']),
    ('kc', 'kc_building_b', 'building-small-b.glb', ['building', 'house']),
    ('kc', 'kc_building_c', 'building-small-c.glb', ['building', 'house']),
    ('kc', 'kc_building_d', 'building-small-d.glb', ['building', 'house']),
    ('kc', 'kc_building_garage', 'building-garage.glb', ['building', 'garage']),
    ('kc', 'kc_road_straight', 'road-straight.glb', ['road', 'ground']),
    ('kc', 'kc_road_corner', 'road-corner.glb', ['road', 'ground']),
    ('kc', 'kc_road_intersection', 'road-intersection.glb', ['road', 'ground']),
    ('kc', 'kc_road_lights', 'road-straight-lightposts.glb', ['road', 'light', 'ground']),
    ('kc', 'kc_pavement', 'pavement.glb', ['ground', 'pavement']),
    ('kc', 'kc_pavement_fountain', 'pavement-fountain.glb', ['ground', 'decor']),
    ('kc', 'kc_grass', 'grass.glb', ['grass', 'nature', 'plant']),
    ('kc', 'kc_grass_trees', 'grass-trees.glb', ['tree', 'nature']),
    ('kc', 'kc_grass_trees_tall', 'grass-trees-tall.glb', ['tree', 'nature']),
    # -- kenney fps kit -------------------------------------------------------
    ('kf', 'kf_wall_high', 'wall-high.glb', ['wall', 'barrier', 'site']),
    ('kf', 'kf_wall_low', 'wall-low.glb', ['wall', 'barrier', 'site']),
    ('kf', 'kf_platform', 'platform.glb', ['platform', 'ground']),
    ('kf', 'kf_platform_grass', 'platform-large-grass.glb', ['platform', 'ground']),
    ('kf', 'kf_grass', 'grass.glb', ['grass', 'nature', 'plant']),
    ('kf', 'kf_grass_small', 'grass-small.glb', ['grass', 'nature', 'plant_stage1']),
    ('kf', 'kf_cloud', 'cloud.glb', ['cloud', 'sky']),
    ('kf', 'kf_drone', 'enemy-flying.glb', ['drone', 'enemy', 'bot']),
    # -- kenney platformer ----------------------------------------------------
    ('kp', 'kp_brick', 'brick.glb', ['block', 'brick']),
    ('kp', 'kp_platform_medium', 'platform-medium.glb', ['platform', 'ground']),
    ('kp', 'kp_platform_large', 'platform-large.glb', ['platform', 'ground']),
    ('kp', 'kp_coin', 'coin.glb', ['coin', 'valuable', 'small']),
    ('kp', 'kp_flag', 'flag.glb', ['flag', 'decor']),
    ('kp', 'kp_dust', 'dust.glb', ['fx', 'particle']),
]

# handy semantic shortcuts used by the Unity scene builder
ROLE_KEYS = {
    'wall_interior': ['pb_wall', 'dg_wall', 'pb_wall_half'],
    'floor_interior': ['pb_floor', 'dg_floor_tile', 'pb_floor_prototype'],
    'door': ['pb_door_a', 'pb_door_b'],
    'doorway': ['pb_wall_doorway', 'dg_wall_doorway'],
    'window': ['pb_wall_window_open', 'dg_wall_window_open'],
    'pillar': ['pb_pillar_a', 'pb_pillar_b', 'dg_column'],
    'stairs': ['pb_stairs', 'dg_stairs', 'ka_stairs'],
    'crate': ['sb_cargo_a', 'pb_box_a', 'dg_box_small', 'sb_containers_a'],
    'crate_large': ['dg_box_large', 'sb_cargodepot_a', 'dg_crates_stacked'],
    'barrel': ['pb_barrel_a', 'pb_barrel_b', 'dg_barrel_large'],
    'container': ['sb_containers_a', 'sb_containers_b', 'sb_cargo_a_stacked'],
    'building': ['kc_building_a', 'kc_building_b', 'kc_building_c', 'kc_building_d'],
    'tree': ['ka_tree', 'kc_grass_trees', 'kc_grass_trees_tall'],
    'grass': ['kf_grass', 'kc_grass', 'kf_grass_small'],
    'plant_stage1': ['kf_grass_small'],
    'plant_stage2': ['kf_grass'],
    'plant_stage3': ['kc_grass'],
    'plant_stage4': ['kc_grass_trees'],
    'plant_stage5': ['kc_grass_trees_tall'],
    'bed': ['dg_bed_frame', 'dg_bed_decorated'],
    'shelf': ['dg_shelf_large', 'dg_shelf_small'],
    'table': ['dg_table_small', 'pb_table_medium'],
    'chest': ['dg_chest', 'dg_chest_gold'],
    'trunk': ['dg_trunk_small', 'dg_trunk_medium', 'dg_trunk_large'],
    'light': ['sb_lights', 'dg_torch'],
    'rack': ['ka_weapon_rack'],
    'module': ['sb_basemodule_a', 'sb_basemodule_b', 'sb_basemodule_c'],
    'roof': ['sb_roofmodule_base', 'sb_roofmodule_cargo_a'],
    'tunnel': ['sb_tunnel_straight_a', 'sb_tunnel_diagonal_long_a'],
    'target': ['pb_wall_target', 'pb_target_small', 'pb_target_stand_a'],
    'vehicle': ['sb_spacetruck', 'sb_spacetruck_large'],
    'furniture': ['dg_shelf_large', 'dg_table_small', 'dg_bed_frame', 'dg_chair'],
    'storage': ['dg_shelf_large', 'dg_trunk_medium', 'dg_wall_shelves'],
    'cover': ['sb_containers_a', 'sb_cargodepot_a', 'dg_crates_stacked', 'sb_spacetruck'],
    'road': ['kc_road_straight', 'kc_road_corner', 'kc_road_intersection'],
    'pavement': ['kc_pavement'],
    'wall_site': ['kf_wall_high', 'kf_wall_low', 'sb_structure_low'],
    'terrain': ['sb_terrain_low', 'sb_terrain_mining'],
    'rock': ['sb_rock_a', 'sb_rocks_a'],
    'pallet': ['pb_pallet_large', 'pb_pallet_small'],
    'machine': ['sb_drill_structure', 'sb_solarpanel'],
    'decor': ['ka_banner', 'ka_statue', 'kp_flag'],
    'drone': ['kf_drone'],
}


def bake_group_props(models, manifest):
    for (grp, key, rel, tags) in PROPS:
        info = GROUPS[grp]
        src = os.path.join(GROUPS[grp]['obj_dir'], rel) if 'obj_dir' in info else None
        if src is None:
            base = {'sb': SB, 'pb': PB, 'dg': DG, 'ka': KA, 'kc': KC, 'kf': KF, 'kp': KP}[grp]
            src = os.path.join(base, rel)
        if not os.path.exists(src):
            print('  !! missing', src)
            continue
        ext = os.path.splitext(src)[1].lower()
        out_dir = os.path.join(OUT, 'Props')
        if ext == '.obj':
            pack, v, t = bp.obj_to_pack(src, key, info['texname'])
        else:
            pack, v, t = bp.gltf_to_pack(src, key, info['texname'])
        lo, hi, _, _ = bp.bounds_of(pack)
        out = os.path.join(out_dir, key + '.expak')
        pack.write(out)
        manifest.append(dict(key=key, file='Props/%s.expak' % key, group=grp, category='prop',
                             tags=tags, size=[round(hi[i] - lo[i], 3) for i in range(3)],
                             min=[round(lo[i], 3) for i in range(3)], max=[round(hi[i], 3) for i in range(3)],
                             verts=v, tris=t, bytes=os.path.getsize(out),
                             source=info['source']))


def main():
    t0 = time.time()
    if os.path.isdir(OUT):
        shutil.rmtree(OUT)
    manifest = []
    print('=== weapons ===')
    for (key, src, kind) in WEAPONS:
        if not os.path.exists(src):
            print('  !! missing', src)
            continue
        grp = 'kf' if 'Starter-Kit-FPS' in src else 'kk'
        texname = GROUPS['kf']['texname'] if grp == 'kf' else 'kaykit_weapon_texture.png'
        if src.endswith('.obj'):
            pack, v, t = bp.obj_to_pack(src, key, texname)
        else:
            # resolve the texture from the glTF document itself
            import gltf_lib as gl
            doc = gl.Gltf(src)
            texname = key + '_tex.png'
            if doc.json.get('images'):
                raw = doc.image_bytes(0)
                os.makedirs(os.path.join(OUT, 'Weapons'), exist_ok=True)
                with open(os.path.join(OUT, 'Weapons', texname), 'wb') as fh:
                    fh.write(raw)
            pack, v, t = bp.gltf_to_pack(src, key, texname)
        lo, hi, _, _ = bp.bounds_of(pack)
        out = os.path.join(OUT, 'Weapons', key + '.expak')
        pack.write(out)
        manifest.append(dict(key=key, file='Weapons/%s.expak' % key, group=grp, category='weapon',
                             tags=['weapon', kind], size=[round(hi[i] - lo[i], 3) for i in range(3)],
                             min=[round(lo[i], 3) for i in range(3)], max=[round(hi[i], 3) for i in range(3)],
                             verts=v, tris=t, bytes=os.path.getsize(out),
                             source='Kenney Starter Kit FPS (CC0)' if grp == 'kf' else 'KayKit Adventurers (CC0)'))
        print('  %-18s verts=%5d tris=%5d size=%s' % (key, v, t, tuple(round(hi[i] - lo[i], 2) for i in range(3))))

    print('=== characters ===')
    for (key, src, tex, display) in CHARACTERS:
        if not os.path.exists(src):
            print('  !! missing', src)
            continue
        bc.bake_character(src, os.path.join(OUT, 'Characters'), key, tex)
        size = os.path.getsize(os.path.join(OUT, 'Characters', key + '.expak'))
        manifest.append(dict(key=key, file='Characters/%s.expak' % key, group='kaykit_characters',
                             category='character', tags=['character', display.lower()],
                             size=[0, 0, 0], verts=0, tris=0, bytes=size,
                             source='KayKit Adventurers / Kenney (CC0)'))

    print('=== props ===')
    bake_group_props(PROPS, manifest)
    print('=== textures ===')
    for grp, info in GROUPS.items():
        if not info['texname'] or not os.path.exists(info['tex']):
            continue
        bp.copy_texture(info['tex'], os.path.join(OUT, 'Props'), info['texname'])
        print('  %s -> Props/%s' % (grp, info['texname']))

    with open(os.path.join(OUT, 'manifest.json'), 'w') as fh:
        json.dump(dict(generated=time.strftime('%Y-%m-%d'), models=manifest, roles=ROLE_KEYS), fh, indent=1)
    total = sum(m['bytes'] for m in manifest)
    print('\n%d models, %.1f MB total, %.1fs' % (len(manifest), total / 1048576.0, time.time() - t0))


if __name__ == '__main__':
    main()
