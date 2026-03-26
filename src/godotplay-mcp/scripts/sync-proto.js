#!/usr/bin/env node
// Copies the canonical proto definition from the repo root into this package's
// proto/ directory so that the published npm package contains an up-to-date copy.
import { readFileSync, writeFileSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const src = resolve(__dirname, '../../../proto/godotplay.proto');
const dest = resolve(__dirname, '../proto/godotplay.proto');

const header =
  '// NOTE: This file is synced from the canonical ../../proto/godotplay.proto at build time.\n' +
  '// Do not edit here directly — edit the root proto file instead.\n';

const canonical = readFileSync(src, 'utf8');
writeFileSync(dest, header + canonical, 'utf8');
console.log('Synced proto/godotplay.proto from root.');
