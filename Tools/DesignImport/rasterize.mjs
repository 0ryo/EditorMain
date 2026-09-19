// Build-time only. Usage: node rasterize.mjs <absolute path to installed sharp module>
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require(process.argv[2]);
const root=path.resolve(import.meta.dirname,'../..');
const output=path.join(root,'Assets/UI/SkillSyncDesign');
const map=JSON.parse(fs.readFileSync(path.join(output,'source-map.json'),'utf8'));
fs.mkdirSync(path.join(output,'Sprites'),{recursive:true});
for (const [png,svg] of Object.entries(map.rasters)) {
  await sharp(path.join(root,'Design/skillsync_codex_handoff/assets',svg),{density:144})
    .png().toFile(path.join(output,'Sprites',png));
}
console.log(`Rasterized ${Object.keys(map.rasters).length} individual vector nodes at 2x.`);
