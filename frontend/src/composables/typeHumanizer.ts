export function typeToName(type: string | null | undefined): string | null {
  if (!type) {
    return null;
  }

  const className = type.split(",")[0];
  let objectName = className.split(".").pop() || "";
  objectName = objectName.replace(/\+/g, ".");

  return objectName;
}
