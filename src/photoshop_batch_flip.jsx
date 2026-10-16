#target photoshop

(function () {
    var originalDisplayDialogs = app.displayDialogs;
    var originalPlaybackDialogs = app.playbackDisplayDialogs;
    var resultFile = null;
    var logLines = [];

    function writeResult(status) {
        if (!resultFile) return;
        logLines.unshift("STATUS\t" + status);
        resultFile.encoding = "UTF8";
        resultFile.open("w");
        resultFile.write(logLines.join("\n"));
        resultFile.close();
    }

    function normalizedPath(value) {
        return String(value).replace(/\\/g, "/").toLowerCase();
    }

    function getDocumentByPath(path) {
        var wanted = normalizedPath(path);
        for (var index = 0; index < app.documents.length; index++) {
            try {
                if (normalizedPath(app.documents[index].fullName.fsName) === wanted)
                    return app.documents[index];
            } catch (ignored) {}
        }
        return null;
    }

    function verifyAction(actionSetName, actionName) {
        var actionRef = new ActionReference();
        actionRef.putName(stringIDToTypeID("action"), actionName);
        actionRef.putName(stringIDToTypeID("actionSet"), actionSetName);
        var actionDesc = executeActionGet(actionRef);
        var stepCount = actionDesc.getInteger(stringIDToTypeID("numberOfChildren"));
        if (stepCount !== 1)
            throw new Error("动作“" + actionSetName + " → " + actionName + "”必须只有一个翻转步骤；当前有 " + stepCount + " 步。");

        var stepRef = new ActionReference();
        stepRef.putIndex(stringIDToTypeID("command"), 1);
        stepRef.putName(stringIDToTypeID("action"), actionName);
        stepRef.putName(stringIDToTypeID("actionSet"), actionSetName);
        var stepDesc = executeActionGet(stepRef);
        var stepName = stepDesc.getString(stringIDToTypeID("name"));
        var lowerStepName = stepName.toLowerCase();
        if (stepName.indexOf("翻转") < 0 && lowerStepName.indexOf("flip") < 0)
            throw new Error("动作的唯一步骤不是翻转操作：" + stepName);
    }

    try {
        var pointerFile = new File(Folder.temp.fsName + "/PS图片批量水平翻转/current_job.txt");
        if (!pointerFile.exists)
            throw new Error("找不到当前批处理任务指针。");
        pointerFile.encoding = "UTF8";
        if (!pointerFile.open("r"))
            throw new Error("无法读取当前批处理任务指针。");
        var jobPath = pointerFile.read().replace(/^\uFEFF/, "").replace(/[\r\n]+$/, "");
        pointerFile.close();

        var jobFile = new File(jobPath);
        if (!jobFile.exists)
            throw new Error("批处理任务文件不存在：" + jobFile.fsName);

        jobFile.encoding = "UTF8";
        if (!jobFile.open("r"))
            throw new Error("无法读取批处理任务文件。");
        var raw = jobFile.read();
        jobFile.close();

        var lines = raw.split(/\r\n|\n|\r/);
        if (lines.length < 7)
            throw new Error("批处理任务文件内容不完整。");
        lines[0] = lines[0].replace(/^\uFEFF/, "");
        if (lines[0] !== "PS_FLIP_JOB_V1")
            throw new Error("无法识别批处理任务文件版本。");

        var actionSetName = lines[1];
        var actionName = lines[2];
        resultFile = new File(lines[3]);
        var batchErrorFile = new File(lines[4]);
        var fileCount = parseInt(lines[5], 10);
        if (!fileCount || lines.length < 6 + fileCount)
            throw new Error("批处理图片列表不完整。");

        verifyAction(actionSetName, actionName);

        var files = [];
        for (var fileIndex = 0; fileIndex < fileCount; fileIndex++) {
            var imageFile = new File(lines[6 + fileIndex]);
            if (!imageFile.exists)
                throw new Error("找不到待处理图片：" + imageFile.fsName);
            files.push(imageFile);
        }

        app.displayDialogs = DialogModes.NO;
        app.playbackDisplayDialogs = DialogModes.NO;

        var batchOptions = new BatchOptions();
        batchOptions.destination = BatchDestinationType.NODESTINATION;
        batchOptions.overrideOpen = false;
        batchOptions.overrideSave = false;
        batchOptions.suppressOpen = true;
        batchOptions.suppressProfile = true;
        batchOptions.errorFile = batchErrorFile;

        app.batch(files, actionName, actionSetName, batchOptions);

        var savedCount = 0;
        for (var saveIndex = 0; saveIndex < files.length; saveIndex++) {
            var document = getDocumentByPath(files[saveIndex].fsName);
            if (!document) {
                logLines.push("ERROR\t批处理后未找到文档：" + files[saveIndex].fsName);
                continue;
            }
            try {
                app.activeDocument = document;
                document.save();
                document.close(SaveOptions.DONOTSAVECHANGES);
                savedCount++;
            } catch (saveError) {
                logLines.push("ERROR\t保存失败：" + files[saveIndex].fsName + "；" + saveError.message);
            }
        }

        logLines.push("PROCESSED\t" + savedCount);
        if (savedCount !== files.length)
            throw new Error("只有 " + savedCount + "/" + files.length + " 张图片成功保存并关闭。");

        writeResult("OK");
    } catch (error) {
        logLines.push("ERROR\t" + error.message + "；line=" + error.line);
        writeResult("ERROR");
    } finally {
        app.displayDialogs = originalDisplayDialogs;
        app.playbackDisplayDialogs = originalPlaybackDialogs;
    }
}());
