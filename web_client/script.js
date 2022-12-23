var port = 7074;
let obj = {
    'id1': -1,
    'id2': -1,
};

(async function enter () {
    await load()
    let add1 = document.getElementById('add1')
    add1.addEventListener('change', fileUpload)
    let add2 = document.getElementById('add2')
    add2.addEventListener('change', fileUpload)
})()


async function load () {
    clearLists()
    let response = await fetch(`https://localhost:${port}/images`)
    let images = await response.json()
    let list1 = document.getElementsByClassName('list1')[0]
    let list2 = document.getElementsByClassName('list2')[0]

    for (let image of images) {
        let insertDiv = document.createElement('div')
        insertDiv.setAttribute('class', 'listItem')
        let titleSpan = document.createElement('span')
        titleSpan.setAttribute('class', 'itemTitle')
        titleSpan.innerText = image.name
        let img = document.createElement('img')
        img.setAttribute('src', 'data:image/png;base64,' + image.details.blob)
        img.setAttribute('alt', '')
        img.setAttribute('class', 'itemImg')
        insertDiv.appendChild(titleSpan)
        insertDiv.appendChild(img)

        insertDiv.id = image.photoId;
        insertDiv.obj = obj
        insertDiv.addEventListener('click', list1ItemSelected)

        let insertDiv2 = insertDiv.cloneNode(true)
        insertDiv2.id = image.photoId;
        insertDiv2.obj = obj
        insertDiv2.addEventListener('click', list2ItemSelected)
        list1.appendChild(insertDiv)
        list2.appendChild(insertDiv2)
    }
}

async function list1ItemSelected(par) {
    let id1 = par.currentTarget.id
    let obj = par.currentTarget.obj

    let targeted = par.target.closest('.listItem')
    let rootDiv = targeted.closest('.list1')
    let children = rootDiv.children
    for (let child of children) {
        child.classList.remove('current')
    }

    targeted.classList.add('current')

    if (obj.id2 != -1) {
        let SimilarityBody = document.getElementsByClassName('SimilarityBody')[0]
        let DistanceBody = document.getElementsByClassName('DistanceBody')[0]
        let id2 = obj.id2

        let response = await fetch(`https://localhost:${port}/compare/${id1}/${id2}`)
        let res = await response.json()

        DistanceBody.innerText = res[0].toLocaleString(undefined, { maximumFractionDigits: 5, minimumFractionDigits: 5})
        SimilarityBody.innerText = res[1].toLocaleString(undefined, { maximumFractionDigits: 5, minimumFractionDigits: 5})
    }
    obj.id1 = id1
}

async function list2ItemSelected(par) {
    let id2 = par.currentTarget.id
    let obj = par.currentTarget.obj

    let targeted = par.target.closest('.listItem')
    let rootDiv = targeted.closest('.list2')
    let children = rootDiv.children
    for (let child of children) {
        child.classList.remove('current2')
    }

    targeted.classList.add('current2')

    if (obj.id1 != -1) {
        let SimilarityBody = document.getElementsByClassName('SimilarityBody')[0]
        let DistanceBody = document.getElementsByClassName('DistanceBody')[0]
        let id1 = obj.id1

        let response = await fetch(`https://localhost:${port}/compare/${id1}/${id2}`)
        let res = await response.json()

        DistanceBody.innerText = res[0].toLocaleString(undefined, { maximumFractionDigits: 5, minimumFractionDigits: 5})
        SimilarityBody.innerText = res[1].toLocaleString(undefined, { maximumFractionDigits: 5, minimumFractionDigits: 5})
    }
    obj.id2 = id2
}

async function fileUpload(event) {
    let files = event.target.files
    let elemId = event.target.getAttribute('id')
    let images = []
    for (let file of files) {
        let prms = new Promise(res => {
            let reader = new FileReader()
            reader.onloadend = e => res(e.target.result)
            reader.readAsDataURL(file)
        })
        let img = (await prms).split(',')[1]

        images.push(
        {
            "photoId": 0,
            "name": file.name,
            "path": file.name,
            "imageHash": -1,
            "details": {
              "photoId": 0,
              "blob": img
            },
            "embeddings": "0000"
        })
    }
    const post_request = {
        mode: 'cors',
        method: 'POST',
        body: JSON.stringify( 
            images
        ),
        headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json'
        }
    }

    let response = await fetch(`https://localhost:${port}/images`, post_request)
    let Ids = await response.json()
    
    // clearDistanceSimilarity()

    let list1 = document.getElementsByClassName('list1')[0]
    let list2 = document.getElementsByClassName('list2')[0]

    for (let i = 0; i < Ids.length; i++) {
        let insertDiv = document.createElement('div')
        insertDiv.setAttribute('class', 'listItem')
        let titleSpan = document.createElement('span')
        titleSpan.setAttribute('class', 'itemTitle')
        titleSpan.innerText = images[i].name
        let img = document.createElement('img')
        img.setAttribute('src', 'data:image/png;base64,' + images[i].details.blob)
        img.setAttribute('alt', '')
        img.setAttribute('class', 'itemImg')
        insertDiv.appendChild(titleSpan)
        insertDiv.appendChild(img)

        insertDiv.id = Ids[i];
        insertDiv.obj = obj

        if (elemId == 'add1') {
            insertDiv.addEventListener('click', list1ItemSelected)
            list1.appendChild(insertDiv)
        }
        else {
            insertDiv.addEventListener('click', list2ItemSelected)
            list2.appendChild(insertDiv)
        }
    }
}

async function clearLists() {
    await clearList1()
    await clearList2()
}

async function clearList1() {
    obj.id1 = -1; obj.id2 = -1;
    document.getElementsByClassName('list1')[0].innerHTML = ''
    clearDistanceSimilarity()
}

async function clearList2() {
    obj.id1 = -1; obj.id2 = -1;
    document.getElementsByClassName('list2')[0].innerHTML = ''
    clearDistanceSimilarity()
}

async function clearDistanceSimilarity() {
    obj.id1 = -1; obj.id2 = -1;
    document.getElementsByClassName('DistanceBody')[0].innerHTML = ''
    document.getElementsByClassName('SimilarityBody')[0].innerHTML = ''
}

async function deleteImages ()  {
    const post_request = {
        mode: 'cors',
        method: 'DELETE',
    }
    let response = await fetch(`https://localhost:${port}/images`, post_request)
    clearLists()
}

