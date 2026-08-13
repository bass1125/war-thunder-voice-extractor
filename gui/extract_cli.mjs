// 战争雷霆语音提取 CLI — 供原生 GUI 调用
// 用法: node extract_cli.mjs <bank路径> <输出目录> <rename 0/1>
import { readFileSync, writeFileSync, mkdirSync } from 'fs';
import { join, resolve } from 'path';
import { parseFsb5, decodeSampleToBlob, loadFmodVorbisSetupPackets } from '@tootallnate/fsb5';

const ROOT = resolve(import.meta.dirname, '..');
const [bankPath, outDir, renameArg] = process.argv.slice(2);
const renameCn = renameArg === '1';

const setupBin = readFileSync(join(ROOT, 'node_modules', '@tootallnate', 'fsb5', 'assets', 'fmod_vorbis_setup_packets.bin'));
const vorbisLib = loadFmodVorbisSetupPackets(setupBin);

const trans = [
  ['^shot$','开火'], ['reload','装填'], ['bail','弃车'], ['understood','收到'], ['acknowledged','收到'],
  ['bracketed','夹击'], ['load_AP','装穿甲弹'], ['load_HEAT','装破甲弹'], ['load_HESH','装碎甲弹'], ['load_HE','装高爆弹'],
  ['load_frag','装榴霰弹'], ['load_canister','装霰弹'], ['load_smoke','装烟雾弹'],
  ['load_flares','装干扰弹'], ['load_missile','装导弹'], ['load_sabot','装脱壳穿甲弹'],
  ['move_forward','前进'], ['move_backward','倒车'], ['move_faster','开快点'], ['move_slower','开慢点'],
  ['move_stop','停车'], ['move_revvs','轰油门'], ['engine_start','引擎启动'], ['engine_stop','引擎熄火'],
  ['engine_prepare','引擎准备'], ['fuel_leak','油箱漏了'], ['crew_lost','成员阵亡'], ['weve_been_hit','我们被击中了'],
  ['barrel_damaged','炮管受损'], ['wheel_lost','轮子掉了'], ['fire_engine','引擎起火'], ['fire_munition','弹药架起火'],
  ['stunned','被震晕'], ['unconscious','失去意识'], ['controls_damaged','操纵装置损坏'], ['controls_repaired','操纵装置修复'],
  ['night_vision_on','夜视仪开'], ['night_vision_off','夜视仪关'],
  ['stabilizer_on','稳定器开'], ['stabilizer_off','稳定器关'], ['stabilizer_repaired','稳定器修复'],
  ['shovel_on','推土铲开'], ['shovel_off','推土铲关'],
  ['art_request','请求炮击'], ['art_on_me','炮击我这里'], ['art_destroy','炮击毁目标'], ['art_coordinates','报炮击坐标'],
  ['art_barrage','弹幕炮击'], ['battle_start','战斗开始'], ['nucav_launch','无人机起飞'], ['nucav_recover','回收无人机'],
  ['correction_left','弹道偏左'], ['correction_right','弹道偏右'], ['nice_shot','目标已毁打得好'],
  ['target_abrams','报敌艾布拉姆斯'], ['target_leopard','报敌豹式'], ['target_tiger','报敌虎式'], ['target_panther','报敌黑豹'],
  ['target_merkava','报敌梅卡瓦'], ['target_leclerc','报敌勒克莱尔'], ['target_t34','报敌T34'], ['target_t64','报敌T64'],
  ['target_t72','报敌T72'], ['target_t80','报敌T80'], ['target_bmp','报敌BMP'], ['target_apc','报敌装甲车'],
  ['target_spaa','报敌自行防空'], ['target_sam','报敌防空导弹'], ['target_mlrs','报敌火箭炮'],
  ['target_helicopter','报敌直升机'], ['target_aircraft','报敌飞机'], ['target_tank','报敌坦克'],
  ['target_vehicle','报敌载具'], ['target_car','报敌汽车'], ['target_enemy','报敌'],
  ['target_on_the_move','报敌移动中'], ['target_near','报敌近距离'], ['target_distance','报敌距离']
];
function Tr(s) {
  if (/^art_(\d+|[A-J])$/.test(s)) return '炮击区域' + s.replace('art_', '');
  const mh = s.match(/^target_(\d+)_h$/); if (mh) return '报敌' + mh[1] + '点钟';
  const mm = s.match(/^target_(\d+)_m$/); if (mm) return '报敌' + mm[1] + '米';
  const mc = s.match(/^correction_(\d+)$/); if (mc) return '弹道修正' + mc[1];
  for (const [p, r] of trans) { if (s.match(p)) return s.replace(new RegExp(p), r); }
  return s;
}
const roleNames = { commander:'车长', driver:'驾驶员', gunner:'炮手', loader:'装填手', chief:'车长助手', chief_m:'车长助手', artillery:'火炮支援', aviation:'航空' };
function cnName(rawName, used) {
  const base = rawName.replace(/^voice_message_/, '');
  const role = base.split('_')[0];
  const roleCn = roleNames[role] || role;
  let rest = base.replace(new RegExp('^' + role + '_'), '');
  let variant = '';
  const m = rest.match(/^(.*)_v(\d+)(?:_(\d+))?$/);
  if (m) { rest = m[1]; variant = '_v' + m[2] + (m[3] ? '_' + m[3] : ''); }
  const cn = Tr(rest).replace(/[\\/:*?"<>|]/g, '_');
  let nb = roleCn + '_' + cn + variant;
  const key = nb.toLowerCase();
  if (used[key] !== undefined) { used[key]++; nb += '_' + used[key]; } else { used[key] = 1; }
  return nb;
}

function emit(o) { console.log(JSON.stringify(o)); }

try {
  const fsbMagic = (readFileSync(bankPath)).indexOf(Buffer.from('FSB5'));
  if (fsbMagic < 0) throw new Error('该文件里没找到 FSB5 音频数据');
  const fsb = parseFsb5(readFileSync(bankPath).subarray(fsbMagic));
  mkdirSync(outDir, { recursive: true });
  const used = {};
  let ok = 0, fail = 0;
  emit({ total: fsb.samples.length });
  for (const s of fsb.samples) {
    try {
      const { blob, extension } = await decodeSampleToBlob(s, fsb.header.mode, vorbisLib);
      const name = renameCn ? cnName(s.name, used) + '.' + extension : s.name + '.' + extension;
      writeFileSync(join(outDir, name), Buffer.from(await blob.arrayBuffer()));
      ok++;
    } catch (e) { fail++; }
    if ((ok + fail) % 25 === 0 || ok + fail === fsb.samples.length) {
      emit({ done: ok + fail, total: fsb.samples.length, current: s.name, ok, fail });
    }
  }
  emit({ done: fsb.samples.length, total: fsb.samples.length, finished: true, ok, fail });
} catch (e) {
  emit({ error: String(e.message || e) });
}