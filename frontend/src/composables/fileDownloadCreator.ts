export function downloadFileFromString(text: string, fileType: string, fileName: string) {
  const fileBlob = new Blob([text], { type: fileType });
  const url = URL.createObjectURL(fileBlob);
  downloadFile(url, fileType, fileName);
}

export async function downloadFileFromResponse(response: Response, fileType: string, fileName: string) {
  const fileBlob = await response.blob();
  const url = URL.createObjectURL(new Blob([fileBlob], { type: fileType }));
  downloadFile(url, fileType, fileName);
}

function downloadFile(url: string, fileType: string, fileName: string) {
  const link = document.createElement("a");
  link.href = url;
  link.setAttribute("download", fileName);
  link.dataset.downloadurl = [fileType, link.download, link.href].join(":");
  link.style.display = "none";
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  setTimeout(() => {
    URL.revokeObjectURL(link.href);
  }, 1500);
}
