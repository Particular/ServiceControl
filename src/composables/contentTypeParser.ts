import { CodeLanguage } from "@/components/codeEditorTypes";

function parseContentType(contentType: string | undefined): { isSupported: boolean; language?: CodeLanguage } {
  if (contentType === undefined) {
    return {
      isSupported: false,
    };
  }

  // remove content type parameter, e.g. charset=utf-8
  contentType = contentType.split(";")[0].trim();

  if (contentType === "application/json") {
    return {
      isSupported: true,
      language: "json",
    };
  }

  if (contentType === "text/xml") {
    return {
      isSupported: true,
      language: "xml",
    };
  }

  if (contentType.startsWith("text/")) {
    return {
      isSupported: true,
    };
  }

  if (contentType === "application/xml") {
    return {
      isSupported: true,
      language: "xml",
    };
  }

  if (contentType.startsWith("application/")) {
    // Some examples:
    // application/atom+xml
    // application/ld+json
    // application/vnd.masstransit+json
    if (contentType.endsWith("+json")) {
      return {
        isSupported: true,
        language: "json",
      };
    } else if (contentType.endsWith("+xml")) {
      return {
        isSupported: true,
        language: "xml",
      };
    }
  }

  return {
    isSupported: false,
  };
}

export default parseContentType;
