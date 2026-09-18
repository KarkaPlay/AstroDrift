mergeInto(LibraryManager.library,
{
	AstroDriftLangRequest_js: function ()
	{
		if (ysdk !== null){
			var returnStr = ysdk.environment.i18n.lang;
			var bufferSize = lengthBytesUTF8(returnStr) + 1;
			var buffer = _malloc(bufferSize);
			stringToUTF8(returnStr, buffer, bufferSize);
			return buffer;
		}
		else {
			return '';
		}
	}
});
