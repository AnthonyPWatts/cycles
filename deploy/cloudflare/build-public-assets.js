import { promises as fs } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const cloudflareDirectory = path.dirname(fileURLToPath(import.meta.url));

export const sourceRoot = path.resolve(cloudflareDirectory, "../../src/Cycles.Api/wwwroot");
export const outputRoot = path.resolve(cloudflareDirectory, ".public-assets");

export const publicFiles = Object.freeze([
  "index.html",
  "privacy.html",
  "site.css",
  "media/cycles-promo.mp4",
  "media/cycles-promo-poster.jpg"
]);

export const publicDirectoryRules = Object.freeze([
  Object.freeze({ directory: "assets/admirals/portraits", extensions: Object.freeze([".webp"]) }),
  Object.freeze({ directory: "assets/galaxy", extensions: Object.freeze([".png", ".webp"]) }),
  Object.freeze({ directory: "assets/icons", extensions: Object.freeze([".svg"]) }),
  Object.freeze({ directory: "media/navigation-backgrounds", extensions: Object.freeze([".png", ".webp"]) }),
  Object.freeze({ directory: "media/promo", extensions: Object.freeze([".png"]) }),
  Object.freeze({ directory: "media/resource-backgrounds", extensions: Object.freeze([".png", ".webp"]) })
]);

function normaliseRelativePath(relativePath) {
  return relativePath.split(path.sep).join("/");
}

async function requireRegularFile(absolutePath, relativePath) {
  const stats = await fs.lstat(absolutePath);
  if (!stats.isFile()) {
    throw new Error(`Public asset '${relativePath}' is not a regular file.`);
  }
}

async function collectDirectoryFiles(root, rule) {
  const directoryRoot = path.join(root, ...rule.directory.split("/"));
  const allowedExtensions = new Set(rule.extensions);
  const files = [];

  async function visit(currentDirectory) {
    const entries = await fs.readdir(currentDirectory, { withFileTypes: true });
    entries.sort((left, right) => left.name.localeCompare(right.name, "en"));

    for (const entry of entries) {
      const absolutePath = path.join(currentDirectory, entry.name);
      const relativePath = normaliseRelativePath(path.relative(root, absolutePath));

      if (entry.isSymbolicLink()) {
        throw new Error(`Public asset source '${relativePath}' must not be a symbolic link.`);
      }

      if (entry.isDirectory()) {
        await visit(absolutePath);
        continue;
      }

      if (!entry.isFile()) {
        throw new Error(`Public asset source '${relativePath}' has an unsupported file type.`);
      }

      if (allowedExtensions.has(path.extname(entry.name).toLowerCase())) {
        files.push(relativePath);
      }
    }
  }

  await visit(directoryRoot);
  return files;
}

export async function collectPublicAssets(root = sourceRoot) {
  const resolvedRoot = path.resolve(root);
  const files = [];

  for (const relativePath of publicFiles) {
    await requireRegularFile(
      path.join(resolvedRoot, ...relativePath.split("/")),
      relativePath);
    files.push(relativePath);
  }

  for (const rule of publicDirectoryRules) {
    files.push(...await collectDirectoryFiles(resolvedRoot, rule));
  }

  const uniqueFiles = [...new Set(files)].sort((left, right) => left.localeCompare(right, "en"));
  if (uniqueFiles.length !== files.length) {
    throw new Error("The public asset allowlist selects at least one file more than once.");
  }

  return uniqueFiles;
}

export async function buildPublicAssets({
  source = sourceRoot,
  output = outputRoot
} = {}) {
  const resolvedSource = path.resolve(source);
  const resolvedOutput = path.resolve(output);
  const relativeOutput = path.relative(resolvedSource, resolvedOutput);

  if (relativeOutput === "" || (!relativeOutput.startsWith("..") && !path.isAbsolute(relativeOutput))) {
    throw new Error("The generated public asset directory must be outside the source web root.");
  }

  const assets = await collectPublicAssets(resolvedSource);
  await fs.rm(resolvedOutput, { recursive: true, force: true });
  await fs.mkdir(resolvedOutput, { recursive: true });

  let totalBytes = 0;
  for (const relativePath of assets) {
    const sourcePath = path.join(resolvedSource, ...relativePath.split("/"));
    const destinationPath = path.join(resolvedOutput, ...relativePath.split("/"));
    await fs.mkdir(path.dirname(destinationPath), { recursive: true });
    await fs.copyFile(sourcePath, destinationPath);
    totalBytes += (await fs.stat(sourcePath)).size;
  }

  return { assets, totalBytes };
}

const invokedPath = process.argv[1] ? path.resolve(process.argv[1]) : null;
if (invokedPath === fileURLToPath(import.meta.url)) {
  const result = await buildPublicAssets();
  console.log(
    `Prepared ${result.assets.length} public Cloudflare assets (${result.totalBytes} bytes) in ${outputRoot}.`);
}
